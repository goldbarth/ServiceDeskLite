using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Tickets.GetDashboardSummary;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

internal sealed class InMemoryDashboardRepository : IDashboardRepository
{
    private const string ResolvedStatusPayloadFragment = "\"toStatus\":\"Resolved\"";

    private readonly InMemoryStore _store;

    public InMemoryDashboardRepository(InMemoryStore store)
        => _store = store;

    public Task<DashboardSummaryDto> GetSummaryAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var tickets = _store.SnapshotTickets();

        var newCount       = tickets.Count(t => t.Status == TicketStatus.New);
        var triagedCount   = tickets.Count(t => t.Status == TicketStatus.Triaged);
        var inProgressCount = tickets.Count(t => t.Status == TicketStatus.InProgress);

        var overdueCount = tickets.Count(
            t => t.DueAt != null
                 && t.DueAt < now
                 && t.Status != TicketStatus.Resolved
                 && t.Status != TicketStatus.Closed);

        var cutoff = now.AddDays(-7);
        var auditEvents = _store.SnapshotAuditEvents();
        var resolvedLast7Days = auditEvents.Count(
            e => e.EventType == AuditEventTypes.StatusChanged
                 && e.OccurredAt >= cutoff
                 && e.Payload.Contains(ResolvedStatusPayloadFragment, StringComparison.Ordinal));

        return Task.FromResult(new DashboardSummaryDto(
            NewCount: newCount,
            TriagedCount: triagedCount,
            InProgressCount: inProgressCount,
            OverdueCount: overdueCount,
            ResolvedLast7DaysCount: resolvedLast7Days));
    }
}