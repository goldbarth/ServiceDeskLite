using ServiceDeskLite.Application.Assistant.GetAiDashboard;
using ServiceDeskLite.Application.Tickets.GetDashboardSummary;
using ServiceDeskLite.Contracts.V1.Dashboard;

namespace ServiceDeskLite.Api.Mapping.Dashboard;

public static class DashboardMapping
{
    public static AiDashboardResponse ToResponse(this AiDashboardDto dto)
        => new(
            WindowDays: dto.WindowDays,
            Volume: new TicketVolumeResponse(dto.Volume.TotalTickets, dto.Volume.CreatedInWindow),
            Automation: new AutomationResponse(
                dto.Automation.AiActions, dto.Automation.TotalActions, dto.Automation.Rate),
            Retrieval: new RetrievalResponse(
                DuplicateChecks: dto.Retrieval.DuplicateChecks,
                DuplicateChecksWithMatch: dto.Retrieval.DuplicateChecksWithMatch,
                DuplicateRate: dto.Retrieval.DuplicateRate,
                ConfidenceSamples: dto.Retrieval.ConfidenceSamples,
                AverageConfidence: dto.Retrieval.AverageConfidence,
                SemanticAvailable: dto.Retrieval.SemanticAvailable),
            Tools: [.. dto.Tools.Select(t => new ToolUsageResponse(
                t.ToolName, t.Invocations, t.Errors, t.ErrorRate, t.AverageConfidence))],
            Tokens: new TokenUsageResponse(
                dto.Tokens.ModelTurns, dto.Tokens.InputTokens, dto.Tokens.OutputTokens, dto.Tokens.TotalTokens));

    public static DashboardSummaryResponse ToResponse(this DashboardSummaryDto dto)
        => new(
            NewCount: dto.NewCount,
            TriagedCount: dto.TriagedCount,
            InProgressCount: dto.InProgressCount,
            OverdueCount: dto.OverdueCount,
            ResolvedLast7DaysCount: dto.ResolvedLast7DaysCount);
}
