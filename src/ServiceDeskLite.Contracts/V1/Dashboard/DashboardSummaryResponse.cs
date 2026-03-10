namespace ServiceDeskLite.Contracts.V1.Dashboard;

public sealed record DashboardSummaryResponse(
    int NewCount,
    int TriagedCount,
    int InProgressCount,
    int OverdueCount,
    int ResolvedLast7DaysCount);