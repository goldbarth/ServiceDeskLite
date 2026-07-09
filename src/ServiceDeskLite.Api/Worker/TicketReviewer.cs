using System.Diagnostics;

using Anthropic.Models.Messages;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Api.Assistant.Agent;
using ServiceDeskLite.Api.Observability;
using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Worker;

/// <summary>
/// Reviews one ticket: reads it, decides what it needs, and acts within the policy — asking for
/// missing information, proposing a grounded solution, or referring the decision to a person.
/// </summary>
/// <remarks>
/// Split from <see cref="TicketWorker"/> so that the schedule and the work are separable: the
/// worker owns <em>when</em>, this owns <em>what</em>. A background loop is awkward to test; one
/// method that reviews one ticket is not.
/// <para>
/// Scoped, and expects the scope it lives in to already be autonomous. It does not switch the mode
/// itself: a class that could grant itself the worker's authority by being constructed is not a
/// guardrail.
/// </para>
/// </remarks>
public sealed partial class TicketReviewer
{
    private readonly GetTicketByIdHandler _tickets;
    private readonly AgentLoop _agent;
    private readonly ICurrentUser _currentUser;
    private readonly IAgentActor _actor;
    private readonly AnthropicOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<TicketReviewer> _logger;

    public TicketReviewer(
        GetTicketByIdHandler tickets,
        AgentLoop agent,
        ICurrentUser currentUser,
        IAgentActor actor,
        IOptions<AnthropicOptions> options,
        IClock clock,
        ILogger<TicketReviewer> logger)
    {
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>What one review did, for the log and for the worker's span.</summary>
    /// <param name="Actions">Tools that ran and changed something or read something successfully.</param>
    /// <param name="Referrals">Calls a guard held back for a human. Not failures.</param>
    public readonly record struct ReviewOutcome(int Actions, int Referrals, bool EndedEarly);

    public async Task<ReviewOutcome> ReviewAsync(TicketId ticketId, CancellationToken ct = default)
    {
        if (_actor.Mode is not AgentMode.Autonomous)
        {
            // Reaching here interactively would mean a tool wrote as the worker, or a high-impact
            // action skipped review. Both are worth a loud failure rather than a quiet wrong answer.
            throw new InvalidOperationException(
                "TicketReviewer must run in an autonomous scope. Call AgentActorContext.RunAutonomously() first.");
        }

        var details = await _tickets.HandleAsync(new GetTicketByIdQuery(ticketId), ct);

        if (!details.IsSuccess)
        {
            // Deleted or otherwise gone between the scan and now. Nothing to do, nothing wrong.
            _logger.LogDebug(
                "Ticket worker: ticket {TicketId} unavailable ({Code})", ticketId.Value, details.Error!.Code);
            return new ReviewOutcome(0, 0, EndedEarly: true);
        }

        var ticket = details.Value!;

        using var activity = AssistantInstrumentation.ActivitySource.StartActivity("worker.ticket");
        activity?.SetTag("ticket.id", ticketId.Value);
        activity?.SetTag("ticket.status", ticket.Status.ToString());

        var request = new AgentRequest(
            SystemPrompt,
            [new MessageParam { Role = Role.User, Content = BuildTicketPrompt(ticket, _clock.UtcNow) }],
            _currentUser.Owner,
            _options.MaxTokens);

        var actions = 0;
        var referrals = 0;

        await foreach (var step in _agent.RunAsync(request, ct))
        {
            switch (step)
            {
                case AgentToolResultEvent { Result.IsError: false } tool:
                    actions++;
                    _logger.LogInformation(
                        "Ticket worker: {Tool} on ticket {TicketId}", tool.ToolName, ticketId.Value);
                    break;

                case AgentToolResultEvent { Result.IsError: true } tool:
                    // A guard refusal and a validation error look the same from here, and both mean
                    // the same thing to the ticket: nothing changed.
                    referrals++;
                    _logger.LogInformation(
                        "Ticket worker: {Tool} on ticket {TicketId} did not run: {Reason}",
                        tool.ToolName, ticketId.Value, tool.Result.Content);
                    break;

                case AgentErrorEvent error:
                    // The loop already logged the cause. Nothing is left half-done: every write that
                    // happened went through a handler and committed on its own.
                    _logger.LogWarning(
                        "Ticket worker: review of ticket {TicketId} ended early: {Message}",
                        ticketId.Value, error.Message);
                    activity?.SetStatus(ActivityStatusCode.Error, error.Message);
                    return new ReviewOutcome(actions, referrals, EndedEarly: true);
            }
        }

        activity?.SetTag("worker.actions", actions);
        activity?.SetTag("worker.referrals", referrals);

        _logger.LogInformation(
            "Ticket worker: finished ticket {TicketId} — {Actions} action(s), {Referrals} referred to a human",
            ticketId.Value, actions, referrals);

        return new ReviewOutcome(actions, referrals, EndedEarly: false);
    }
}
