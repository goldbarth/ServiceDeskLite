using Microsoft.EntityFrameworkCore;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Tickets.GetDashboardSummary;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.Persistence.Repositories;

public sealed class EfDashboardRepository : IDashboardRepository
{
    // This substring is guaranteed by AuditEventFactory.FromStatusChanged,
    // which serializes with System.Text.Json (no spaces, camelCase).
    private const string ResolvedStatusPayloadFragment = "\"toStatus\":\"Resolved\"";

    private readonly ServiceDeskLiteDbContext _dbContext;

    public EfDashboardRepository(ServiceDeskLiteDbContext dbContext)
        => _dbContext = dbContext;

    public async Task<DashboardSummaryDto> GetSummaryAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var tickets = _dbContext.Tickets.AsNoTracking();
        var auditEvents = _dbContext.AuditEvents.AsNoTracking();

        var newCount = await tickets.CountAsync(t => t.Status == TicketStatus.New, ct);
        var triagedCount = await tickets.CountAsync(t => t.Status == TicketStatus.Triaged, ct);
        var inProgressCount = await tickets.CountAsync(t => t.Status == TicketStatus.InProgress, ct);

        var overdueCount = await tickets.CountAsync(
            t => t.DueAt != null
                 && t.DueAt < now
                 && t.Status != TicketStatus.Resolved
                 && t.Status != TicketStatus.Closed,
            ct);

        var cutoff = now.AddDays(-7);
        var resolvedLast7Days = await auditEvents.CountAsync(
            e => e.EventType == AuditEventTypes.StatusChanged
                 && e.OccurredAt >= cutoff
                 && e.Payload.Contains(ResolvedStatusPayloadFragment),
            ct);

        return new DashboardSummaryDto(
            NewCount: newCount,
            TriagedCount: triagedCount,
            InProgressCount: inProgressCount,
            OverdueCount: overdueCount,
            ResolvedLast7DaysCount: resolvedLast7Days);
    }
}