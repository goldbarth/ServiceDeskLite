using ServiceDeskLite.Application.Assistant.GetAiDashboard;

namespace ServiceDeskLite.Application.Abstractions.Persistence;

/// <summary>
/// Aggregates AI operations for the dashboard. Separate from <see cref="IDashboardRepository"/>
/// because it answers a different question from different sources — audit events for what the
/// assistant did to tickets, and the metrics sink for what it cost and how well retrieval scored.
/// </summary>
public interface IAiDashboardRepository
{
    /// <summary>
    /// Returns AI metrics. Windowed figures cover <paramref name="since"/> to
    /// <paramref name="now"/>; total ticket volume is all-time. The reference times are
    /// passed in so callers control them, matching <see cref="IDashboardRepository.GetSummaryAsync"/>.
    /// The window length reported back to the client is derived from the two, so the
    /// caller's choice of window cannot drift from what the numbers actually cover.
    /// </summary>
    Task<AiDashboardDto> GetAsync(
        DateTimeOffset since, DateTimeOffset now, CancellationToken ct = default);
}
