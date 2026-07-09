using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Abstractions.Routing;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.AssignTicket;
using ServiceDeskLite.Application.Tickets.ChangeTicketStatus;
using ServiceDeskLite.Application.Tickets.UpdateTicket;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.Routing;

/// <summary>
/// Applies a routing decision to a ticket, or surfaces it as a suggestion. The router
/// classifies; this use-case owns the confidence gate and the application. Every change
/// goes through the existing command handlers (update, assign, change-status) — never a
/// direct domain write — so routing inherits their validation and audit trail (each
/// raises a domain event → audit record). Below the confidence threshold, nothing is
/// applied and the decision is returned as a suggestion (issue #159, ADR-0032).
/// </summary>
public sealed class RouteTicketHandler
{
    // Below this, routing is too uncertain to apply automatically — suggest instead.
    public const double ApplyThreshold = 0.5;

    private readonly ITicketRepository _tickets;
    private readonly IAgentRepository _agents;
    private readonly ITicketRouter _router;
    private readonly UpdateTicketHandler _update;
    private readonly AssignTicketHandler _assign;
    private readonly ChangeTicketStatusHandler _changeStatus;

    public RouteTicketHandler(
        ITicketRepository tickets,
        IAgentRepository agents,
        ITicketRouter router,
        UpdateTicketHandler update,
        AssignTicketHandler assign,
        ChangeTicketStatusHandler changeStatus)
    {
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _update = update ?? throw new ArgumentNullException(nameof(update));
        _assign = assign ?? throw new ArgumentNullException(nameof(assign));
        _changeStatus = changeStatus ?? throw new ArgumentNullException(nameof(changeStatus));
    }

    public async Task<Result<RouteTicketResult>> HandleAsync(RouteTicketCommand? command, CancellationToken ct = default)
    {
        if (command is null)
            return Result<RouteTicketResult>.Validation("route_ticket.command.null", "Command must not be null.");

        var ticket = await _tickets.GetByIdAsync(command.Id, ct);
        if (ticket is null)
            return Result<RouteTicketResult>.NotFound(
                "ticket.not_found", "Ticket was not found.",
                new Dictionary<string, object> { ["ticketId"] = command.Id }!);

        var decision = _router.Route(ticket.Title, ticket.Description);

        // Low confidence: do not touch the ticket, hand back a suggestion.
        if (decision.Confidence < ApplyThreshold)
            return Result<RouteTicketResult>.Success(new RouteTicketResult(false, decision, []));

        var changes = new List<string>();

        // Priority + category via the partial-update handler (raises DetailsUpdated → audit).
        var update = await _update.HandleAsync(
            new UpdateTicketCommand(command.Id, Priority: decision.Priority, Category: decision.Category, Actor: command.Actor),
            ct);
        if (update.IsSuccess)
            changes.Add($"priority={decision.Priority}, category={decision.Category}");

        // Assignee: resolve the suggested roster name to an active agent, then assign.
        if (decision.SuggestedAssignee is { } assigneeName)
        {
            var agents = await _agents.GetActiveAsync(ct);
            var agent = agents.FirstOrDefault(a => a.Name.Equals(assigneeName, StringComparison.OrdinalIgnoreCase));
            if (agent is not null)
            {
                var assign = await _assign.HandleAsync(
                    new AssignTicketCommand(command.Id, agent.Id, command.Actor), ct);
                if (assign.IsSuccess)
                    changes.Add($"assigned to {agent.Name}");
            }
        }

        // Status: apply the triage move only when it is a valid transition from the current
        // state; a rejected transition is skipped, not fatal to routing.
        if (decision.SuggestedStatus is { } status
            && status != ticket.Status
            && TicketWorkflow.GetAllowedTransitions(ticket.Status).Contains(status))
        {
            var changed = await _changeStatus.HandleAsync(
                new ChangeTicketStatusCommand(command.Id, status, command.Actor), ct);
            if (changed.IsSuccess)
                changes.Add($"status={status}");
        }

        return Result<RouteTicketResult>.Success(new RouteTicketResult(changes.Count > 0, decision, changes));
    }
}
