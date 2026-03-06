using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

    // Filter staging fields — hold what the user has typed/selected in the UI.
    // They are applied to the query only when the user clicks "Suchen" or presses Enter.
    private string? _filterQ;
    private IEnumerable<TicketStatus> _filterStatuses = [];
    private IEnumerable<TicketPriority> _filterPriorities = [];
    private string? _filterAssignee;

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
        SyncFilterFieldsFromQuery(query);
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

    private Task ApplyFiltersAsync()
        => LoadAndSyncUrlAsync(Query with
        {
            Page = 1,
            Q = string.IsNullOrWhiteSpace(_filterQ) ? null : _filterQ.Trim(),
            Statuses = _filterStatuses.Any() ? _filterStatuses.ToArray() : null,
            Priorities = _filterPriorities.Any() ? _filterPriorities.ToArray() : null,
            Assignee = string.IsNullOrWhiteSpace(_filterAssignee) ? null : _filterAssignee.Trim()
        });

    private Task ClearFiltersAsync()
    {
        _filterQ = null;
        _filterStatuses = [];
        _filterPriorities = [];
        _filterAssignee = null;
        return LoadAndSyncUrlAsync(Query with
        {
            Page = 1,
            Q = null,
            Statuses = null,
            Priorities = null,
            Assignee = null
        });
    }

    private async Task OnSearchKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
            await ApplyFiltersAsync();
    }

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
    /// Reads page/pageSize/sort/dir and filter params from the current URL query string.
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

        string? q = qs.TryGetValue("q", out var qv) ? (string?)qv : null;

        TicketStatus[]? statuses = null;
        if (qs.TryGetValue("statuses", out var sv) && sv.Count > 0)
        {
            var parsed = sv
                .Where(s => Enum.TryParse<TicketStatus>(s, out _))
                .Select(s => Enum.Parse<TicketStatus>(s!))
                .ToArray();
            if (parsed.Length > 0) statuses = parsed;
        }

        TicketPriority[]? priorities = null;
        if (qs.TryGetValue("priorities", out var pv) && pv.Count > 0)
        {
            var parsed = pv
                .Where(s => Enum.TryParse<TicketPriority>(s, out _))
                .Select(s => Enum.Parse<TicketPriority>(s!))
                .ToArray();
            if (parsed.Length > 0) priorities = parsed;
        }

        string? assignee = qs.TryGetValue("assignee", out var av) ? (string?)av : null;

        return new TicketQueryParams(page, pageSize, sortField, sortDir, q, statuses, priorities, assignee);
    }

    /// <summary>
    /// Pushes the current query parameters into the browser URL without triggering
    /// a Blazor navigation (replaceHistoryEntry: true keeps the back-button clean).
    /// </summary>
    private void SyncUrl(TicketQueryParams query)
    {
        var defaults = TicketQueryParams.Default;
        var qs = new List<KeyValuePair<string, string?>>();

        if (query.Page != defaults.Page)                   qs.Add(new("page",     query.Page.ToString()));
        if (query.PageSize != defaults.PageSize)           qs.Add(new("pageSize", query.PageSize.ToString()));
        if (query.SortField != defaults.SortField)         qs.Add(new("sort",     query.SortField.ToString()));
        if (query.SortDirection != defaults.SortDirection) qs.Add(new("dir",      query.SortDirection.ToString()));

        if (!string.IsNullOrWhiteSpace(query.Q))
            qs.Add(new("q", query.Q));

        if (query.Statuses is { Length: > 0 })
            foreach (var s in query.Statuses) qs.Add(new("statuses", s.ToString()));

        if (query.Priorities is { Length: > 0 })
            foreach (var pr in query.Priorities) qs.Add(new("priorities", pr.ToString()));

        if (!string.IsNullOrWhiteSpace(query.Assignee))
            qs.Add(new("assignee", query.Assignee));

        var url = QueryHelpers.AddQueryString("/tickets", qs);
        Nav.NavigateTo(url, forceLoad: false, replace: true);
    }

    /// <summary>
    /// Copies filter values from a parsed query into the filter staging fields
    /// so the UI reflects the URL state on initial load.
    /// </summary>
    private void SyncFilterFieldsFromQuery(TicketQueryParams query)
    {
        _filterQ = query.Q;
        _filterStatuses = query.Statuses ?? [];
        _filterPriorities = query.Priorities ?? [];
        _filterAssignee = query.Assignee;
    }

    // Invoked by FeatureState when state changes — must marshal to renderer thread.
    private void HandleStateChanged() => InvokeAsync(StateHasChanged);
}