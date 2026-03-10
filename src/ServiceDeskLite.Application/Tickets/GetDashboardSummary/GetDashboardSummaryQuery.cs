namespace ServiceDeskLite.Application.Tickets.GetDashboardSummary;

/// <summary>
/// Marker query – the dashboard summary has no input parameters.
/// Using an explicit query type keeps the handler signature consistent
/// with all other use cases and makes the null-guard pattern uniform.
/// </summary>
public sealed record GetDashboardSummaryQuery;