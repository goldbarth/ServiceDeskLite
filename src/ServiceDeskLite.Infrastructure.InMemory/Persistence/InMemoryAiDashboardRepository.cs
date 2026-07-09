using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Assistant.GetAiDashboard;
using ServiceDeskLite.Domain.Audit;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

internal sealed class InMemoryAiDashboardRepository : IAiDashboardRepository
{
    private readonly InMemoryStore _store;

    public InMemoryAiDashboardRepository(InMemoryStore store)
        => _store = store;

    public Task<AiDashboardDto> GetAsync(
        DateTimeOffset since, DateTimeOffset now, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var tickets = _store.SnapshotTickets();
        var auditEvents = _store.SnapshotAuditEvents().Where(e => e.OccurredAt >= since).ToList();
        var invocations = _store.SnapshotToolInvocations().Where(i => i.OccurredAt >= since).ToList();
        var tokenUsages = _store.SnapshotTokenUsages().Where(u => u.OccurredAt >= since).ToList();

        var result = new AiDashboardDto(
            WindowDays: (int)Math.Round((now - since).TotalDays),
            Volume: new TicketVolumeDto(
                TotalTickets: tickets.Count,
                CreatedInWindow: tickets.Count(t => t.CreatedAt >= since)),
            Automation: new AutomationDto(
                AiActions: auditEvents.Count(e => e.Actor == AuditActors.AiAssistant),
                TotalActions: auditEvents.Count),
            Retrieval: AiDashboardAggregation.Retrieval(invocations),
            Tools: AiDashboardAggregation.Tools(invocations),
            Tokens: new TokenUsageDto(
                ModelTurns: tokenUsages.Count,
                InputTokens: tokenUsages.Sum(u => u.InputTokens),
                OutputTokens: tokenUsages.Sum(u => u.OutputTokens)));

        return Task.FromResult(result);
    }
}
