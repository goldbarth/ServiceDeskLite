using ServiceDeskLite.Application.Tickets.GetDashboardSummary;

namespace ServiceDeskLite.Application.Abstractions.Persistence;

public interface IDashboardRepository
{
    /// <summary>
    /// Returns aggregated KPI counts for the dashboard.
    /// <paramref name="now"/> is passed explicitly so callers control the reference time
    /// without requiring a clock abstraction.
    /// </summary>
    Task<DashboardSummaryDto> GetSummaryAsync(DateTimeOffset now, CancellationToken ct = default);
}