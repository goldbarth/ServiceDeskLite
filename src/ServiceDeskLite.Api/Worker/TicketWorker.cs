using Microsoft.Extensions.Options;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Api.Observability;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Worker;

/// <summary>
/// Scans open tickets on a schedule and hands each one to <see cref="TicketReviewer"/> (ADR-0037).
/// </summary>
/// <remarks>
/// This class owns <em>when</em> and nothing else: the interval, which tickets are due for a look,
/// and the promise that one bad ticket does not end the loop. What a review actually does, and what
/// it is allowed to do, lives in <see cref="TicketReviewer"/> and
/// <see cref="AutonomousWorkerOptions"/>.
/// <para>
/// The scope opened per ticket is the only place in the process that becomes the autonomous agent.
/// Everything else — every HTTP request — is the interactive assistant by construction.
/// </para>
/// </remarks>
public sealed class TicketWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AutonomousWorkerOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<TicketWorker> _logger;

    public TicketWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AutonomousWorkerOptions> options,
        IClock clock,
        ILogger<TicketWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "AutonomousWorker:Enabled is false — the ticket worker is off and no ticket will be touched.");
            return;
        }

        // The effective policy, not the configured one: an operator narrowing what the worker may do
        // has to be able to see that it took, and a binding that silently kept the defaults is worth
        // finding in the first log line rather than in the audit trail.
        _logger.LogInformation(
            "Autonomous ticket worker started: every {Interval}s, at most {Max} ticket(s) per scan, "
            + "older than {MinAge}min; scanning {Statuses}; unattended writes: {Writes}; "
            + "unattended transitions: {Transitions}",
            _options.ScanIntervalSeconds,
            _options.MaxTicketsPerRun,
            _options.MinTicketAgeMinutes,
            string.Join(", ", _options.ScanStatuses),
            string.Join(", ", _options.AutonomousWrites),
            string.Join(", ", _options.AutonomousStatusTransitions));

        var interval = TimeSpan.FromSeconds(_options.ScanIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The next scan is a fresh attempt. Letting this escape would end the worker for the
                // life of the process, over one bad pass.
                _logger.LogError(ex, "Ticket worker scan failed; retrying at the next interval");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        using var scanActivity = AssistantInstrumentation.ActivitySource.StartActivity("worker.scan");

        var candidates = await FindCandidatesAsync(ct);
        scanActivity?.SetTag("worker.candidates", candidates.Count);

        if (candidates.Count == 0)
        {
            _logger.LogDebug("Ticket worker: no tickets to review");
            return;
        }

        _logger.LogInformation("Ticket worker: reviewing {Count} ticket(s)", candidates.Count);

        foreach (var ticketId in candidates)
        {
            if (ct.IsCancellationRequested)
                return;

            try
            {
                await ReviewAsync(ticketId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ticket worker: reviewing ticket {TicketId} failed", ticketId.Value);
            }
        }
    }

    /// <summary>
    /// Oldest first, so a ticket cannot starve behind a steady arrival of newer ones, and only
    /// tickets that have had time to settle.
    /// </summary>
    private async Task<IReadOnlyList<TicketId>> FindCandidatesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();

        var criteria = new TicketSearchCriteria(
            Statuses: [.. _options.ScanStatuses],
            CreatedTo: _clock.UtcNow.AddMinutes(-_options.MinTicketAgeMinutes));

        var query = new SearchTicketsQuery(
            criteria,
            new Paging(Page: 1, PageSize: _options.MaxTicketsPerRun),
            new SortSpec(TicketSortField.CreatedAt, SortDirection.Asc));

        var result = await search.HandleAsync(query, ct);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Ticket worker: scanning for tickets failed ({Code})", result.Error!.Code);
            return [];
        }

        return [.. result.Value!.Page.Items.Select(t => t.Id)];
    }

    private async Task ReviewAsync(TicketId ticketId, CancellationToken ct)
    {
        // A scope per ticket: the tools, the loop and the RAG retrieval context are all scoped, and
        // one ticket's retrieved passages must never ground another ticket's answer.
        using var scope = _scopeFactory.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<AgentActorContext>().RunAutonomously();

        await services.GetRequiredService<TicketReviewer>().ReviewAsync(ticketId, ct);
    }
}
