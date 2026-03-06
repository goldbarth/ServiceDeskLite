using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

using MudBlazor;

using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Features.Tickets.State;

using SortDirection = ServiceDeskLite.Contracts.V1.Common.SortDirection;

namespace ServiceDeskLite.Web.Features.Tickets.Pages;

public partial class TicketsListPage : IDisposable
{
    [Inject] private TicketsListFeatureState FeatureState { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    // -----------------------------------------------------------------------
    // Derived view properties — read-only projections of FeatureState
    // -----------------------------------------------------------------------

    private TicketsListState State => FeatureState.State;
    private TicketQueryParams Query => FeatureState.Query;

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        FeatureState.OnChanged += HandleStateChanged;

        // If data is fresh and already loaded (user navigated back), skip reload.
        if (FeatureState is { State: TicketsListState.Loaded, IsStale: false })
            return;

        var query = ParseQueryFromUrl();
        await FeatureState.LoadAsync(query);
    }

    public void Dispose()
        => FeatureState.OnChanged -= HandleStateChanged;

    // -----------------------------------------------------------------------
    // Event handlers — delegate everything to FeatureState
    // -----------------------------------------------------------------------

    private Task OnPageChangedAsync(int page)
        => LoadAndSyncUrlAsync(Query with { Page = page });

    private Task OnPageSizeChangedAsync(int pageSize)
    {
        if (Query.PageSize == pageSize) return Task.CompletedTask;
        return LoadAndSyncUrlAsync(Query with { Page = 1, PageSize = pageSize });
    }

    private Task SortByAsync(TicketSortField field)
        => LoadAndSyncUrlAsync(Query.WithSort(field));

    private Task ReloadAsync()
        => LoadAndSyncUrlAsync(Query);

    private void HandleRowClick(TableRowClickEventArgs<TicketListItemResponse> args)
        => Nav.NavigateTo($"/tickets/{args.Item!.Id}");

    // -----------------------------------------------------------------------
    // Rendering helpers
    // -----------------------------------------------------------------------

    private string SortIcon(TicketSortField field)
    {
        if (Query.SortField != field) return string.Empty;
        return Query.SortDirection == SortDirection.Asc
            ? Icons.Material.Filled.ArrowUpward
            : Icons.Material.Filled.ArrowDownward;
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private async Task LoadAndSyncUrlAsync(TicketQueryParams query)
    {
        SyncUrl(query);
        await FeatureState.LoadAsync(query);
    }

    /// <summary>
    /// Reads page/pageSize/sortField/sortDir from the current URL query string.
    /// Falls back to <see cref="TicketQueryParams.Default"/> for any missing parameter.
    /// </summary>
    private TicketQueryParams ParseQueryFromUrl()
    {
        var uri = new Uri(Nav.Uri);
        var qs = QueryHelpers.ParseQuery(uri.Query);
        var defaults = TicketQueryParams.Default;

        var page = qs.TryGetValue("page", out var p) && int.TryParse(p, out var pi) && pi >= 1
            ? pi : defaults.Page;

        var pageSize = qs.TryGetValue("pageSize", out var ps) && int.TryParse(ps, out var psi) && psi is >= 1 and <= 200
            ? psi : defaults.PageSize;

        var sortField = qs.TryGetValue("sort", out var sf) && Enum.TryParse<TicketSortField>(sf, out var sfi)
            ? sfi : defaults.SortField;

        var sortDir = qs.TryGetValue("dir", out var d) && Enum.TryParse<SortDirection>(d, out var di)
            ? di : defaults.SortDirection;

        return new TicketQueryParams(page, pageSize, sortField, sortDir);
    }

    /// <summary>
    /// Pushes the current query parameters into the browser URL without triggering
    /// a Blazor navigation (replaceHistoryEntry: true keeps the back-button clean).
    /// </summary>
    private void SyncUrl(TicketQueryParams query)
    {
        var defaults = TicketQueryParams.Default;
        var qs = new Dictionary<string, string?>();

        if (query.Page != defaults.Page)             qs["page"]     = query.Page.ToString();
        if (query.PageSize != defaults.PageSize)     qs["pageSize"] = query.PageSize.ToString();
        if (query.SortField != defaults.SortField)   qs["sort"]     = query.SortField.ToString();
        if (query.SortDirection != defaults.SortDirection) qs["dir"] = query.SortDirection.ToString();

        var url = QueryHelpers.AddQueryString("/tickets", qs);
        Nav.NavigateTo(url, forceLoad: false, replace: true);
    }

    // Invoked by FeatureState when state changes — must marshal to renderer thread.
    private void HandleStateChanged() => InvokeAsync(StateHasChanged);
}
