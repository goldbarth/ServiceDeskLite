using Microsoft.EntityFrameworkCore;

using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Assistant.GetAiDashboard;
using ServiceDeskLite.Domain.Audit;

namespace ServiceDeskLite.Infrastructure.Persistence.Repositories;

public sealed class EfAiDashboardRepository : IAiDashboardRepository
{
    private readonly ServiceDeskLiteDbContext _dbContext;

    public EfAiDashboardRepository(ServiceDeskLiteDbContext dbContext)
        => _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<AiDashboardDto> GetAsync(
        DateTimeOffset since, DateTimeOffset now, CancellationToken ct = default)
    {
        var volume = await GetVolumeAsync(since, ct);
        var automation = await GetAutomationAsync(since, ct);

        var invocations = await _dbContext.AssistantToolInvocations
            .AsNoTracking()
            .Where(i => i.OccurredAt >= since)
            .Select(i => new AssistantToolInvocation(
                i.ToolName, i.Kind, i.IsError, i.Confidence, i.MatchCount, i.SemanticAvailable, i.OccurredAt))
            .ToListAsync(ct);

        var tokens = await GetTokensAsync(since, ct);

        return new AiDashboardDto(
            WindowDays: (int)Math.Round((now - since).TotalDays),
            Volume: volume,
            Automation: automation,
            Retrieval: AiDashboardAggregation.Retrieval(invocations),
            Tools: AiDashboardAggregation.Tools(invocations),
            Tokens: tokens);
    }

    private async Task<TicketVolumeDto> GetVolumeAsync(DateTimeOffset since, CancellationToken ct)
    {
        var tickets = _dbContext.Tickets.AsNoTracking();

        return new TicketVolumeDto(
            TotalTickets: await tickets.CountAsync(ct),
            CreatedInWindow: await tickets.CountAsync(t => t.CreatedAt >= since, ct));
    }

    private async Task<AutomationDto> GetAutomationAsync(DateTimeOffset since, CancellationToken ct)
    {
        // Every write path — UI, API, assistant — produces an audit event, and the assistant
        // reaches the domain only through those same handlers. Actor is therefore the whole
        // difference between an automated action and a human one.
        var events = _dbContext.AuditEvents.AsNoTracking().Where(e => e.OccurredAt >= since);

        return new AutomationDto(
            AiActions: await events.CountAsync(e => e.Actor == AuditActors.AiAssistant, ct),
            TotalActions: await events.CountAsync(ct));
    }

    private async Task<TokenUsageDto> GetTokensAsync(DateTimeOffset since, CancellationToken ct)
    {
        var usages = _dbContext.AssistantTokenUsages.AsNoTracking().Where(u => u.OccurredAt >= since);

        return new TokenUsageDto(
            ModelTurns: await usages.CountAsync(ct),
            InputTokens: await usages.SumAsync(u => u.InputTokens, ct),
            OutputTokens: await usages.SumAsync(u => u.OutputTokens, ct));
    }
}
