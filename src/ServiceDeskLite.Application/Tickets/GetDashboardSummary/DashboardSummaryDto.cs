namespace ServiceDeskLite.Application.Tickets.GetDashboardSummary;

public sealed record DashboardSummaryDto(
    int NewCount,
    int TriagedCount,
    int InProgressCount,
    int OverdueCount,
    int ResolvedLast7DaysCount);