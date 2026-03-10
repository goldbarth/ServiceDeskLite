using Microsoft.AspNetCore.Components;

using ServiceDeskLite.Contracts.V1.Dashboard;
using ServiceDeskLite.Web.Api.V1;

namespace ServiceDeskLite.Web.Features.Dashboard.Pages;

public partial class DashboardPage
{
    [Inject] private ITicketsApiClient TicketsApi { get; set; } = default!;

    private bool _isLoading;
    private ApiError? _error;
    private DashboardSummaryResponse? _summary;

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;

        var result = await TicketsApi.GetDashboardSummaryAsync();

        if (result.IsSuccess)
            _summary = result.Value;
        else
            _error = result.Error;

        _isLoading = false;
    }
}
