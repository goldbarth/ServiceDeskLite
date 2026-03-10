using ServiceDeskLite.Application.Tickets.GetDashboardSummary;
using ServiceDeskLite.Contracts.V1.Dashboard;

namespace ServiceDeskLite.Api.Mapping.Dashboard;

public static class DashboardMapping
{
    public static DashboardSummaryResponse ToResponse(this DashboardSummaryDto dto)
        => new(
            NewCount: dto.NewCount,
            TriagedCount: dto.TriagedCount,
            InProgressCount: dto.InProgressCount,
            OverdueCount: dto.OverdueCount,
            ResolvedLast7DaysCount: dto.ResolvedLast7DaysCount);
}
