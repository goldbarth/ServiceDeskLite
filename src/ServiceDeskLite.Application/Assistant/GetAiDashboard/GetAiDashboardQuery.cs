namespace ServiceDeskLite.Application.Assistant.GetAiDashboard;

/// <summary>
/// Marker query — the AI dashboard has no input parameters. An explicit query type keeps
/// the handler signature and null-guard uniform with every other use case.
/// </summary>
public sealed record GetAiDashboardQuery;
