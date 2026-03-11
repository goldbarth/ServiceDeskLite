using Microsoft.AspNetCore.Components;

using ServiceDeskLite.Contracts.V1.Dashboard;
using ServiceDeskLite.Web.Api.V1;
using ServiceDeskLite.Web.Features.Dashboard.Components;

namespace ServiceDeskLite.Web.Features.Dashboard.Pages;

public partial class DashboardPage
{
    [Inject] private ITicketsApiClient TicketsApi { get; set; } = default!;

    private bool _isLoading;
    private ApiError? _error;
    private DashboardSummaryResponse? _summary;

    private IReadOnlyList<DashboardHeroStat> HeroStats
        => _summary is null
            ? []
            :
            [
                new("Open workload", (_summary.NewCount + _summary.TriagedCount + _summary.InProgressCount).ToString()),
                new("Overdue", _summary.OverdueCount.ToString()),
                new("Resolved 7d", _summary.ResolvedLast7DaysCount.ToString())
            ];

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;

        try
        {
            var result = await TicketsApi.GetDashboardSummaryAsync();

            if (result.IsSuccess)
                _summary = result.Value;
            else
                _error = result.Error;
        }
        finally
        {
            _isLoading = false;
        }
    }
}
