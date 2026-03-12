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

    private TicketsListState State => FeatureState.State;
    private TicketQueryParams Query => FeatureState.Query;

    private string? _filterQ;
    private IEnumerable<TicketStatus> _filterStatuses = [];
    private IEnumerable<TicketPriority> _filterPriorities = [];
    private string? _filterAssignee;

    private string ActiveFilterCountLabel
    {
        get
        {
            var count = 0;

            if (!string.IsNullOrWhiteSpace(Query.Q))
            {
                count++;
            }

            if (Query.Statuses is { Length: > 0 })
            {
                count++;
            }

            if (Query.Priorities is { Length: > 0 })
            {
                count++;
            }

            if (!string.IsNullOrWhiteSpace(Query.Assignee))
            {
                count++;
            }

            return count == 1 ? "1 active filter" : $"{count} active filters";
        }
    }

    protected override async Task OnInitializedAsync()
    {
        FeatureState.OnChanged += HandleStateChanged;

        if (FeatureState is { State: TicketsListState.Loaded, IsStale: false })
        {
            return;
        }

        var query = ParseQueryFromUrl();
        SyncFilterFieldsFromQuery(query);
        await FeatureState.LoadAsync(query);
    }

    public void Dispose()
        => FeatureState.OnChanged -= HandleStateChanged;

    private Task OnPageChangedAsync(int page)
        => LoadAndSyncUrlAsync(Query with { Page = page });

    private Task OnPageSizeChangedAsync(int pageSize)
    {
        if (Query.PageSize == pageSize)
        {
            return Task.CompletedTask;
        }

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
        {
            await ApplyFiltersAsync();
        }
    }

    private void HandleRowClick(TableRowClickEventArgs<TicketListItemResponse> args)
        => Nav.NavigateTo($"/tickets/{args.Item!.Id}");

    private string SortIcon(TicketSortField field)
    {
        if (Query.SortField != field)
        {
            return Icons.Material.Outlined.UnfoldMore;
        }

        return Query.SortDirection == SortDirection.Asc
            ? Icons.Material.Outlined.ArrowUpward
            : Icons.Material.Outlined.ArrowDownward;
    }

    private string SortButtonClass(TicketSortField field)
        => Query.SortField == field ? "tickets-sort is-active" : "tickets-sort";

    private static string FormatStatus(TicketStatus status)
        => status switch
        {
            TicketStatus.InProgress => "In Progress",
            _ => status.ToString()
        };

    private static string FormatPriority(TicketPriority priority)
        => priority.ToString();

    private static string StatusChipClass(TicketStatus status)
        => status switch
        {
            TicketStatus.New => "tickets-chip--status-new",
            TicketStatus.Triaged => "tickets-chip--status-triaged",
            TicketStatus.InProgress => "tickets-chip--status-inprogress",
            TicketStatus.Waiting => "tickets-chip--status-waiting",
            TicketStatus.Resolved => "tickets-chip--status-resolved",
            TicketStatus.Closed => "tickets-chip--status-closed",
            _ => string.Empty
        };

    private static string PriorityChipClass(TicketPriority priority)
        => priority switch
        {
            TicketPriority.Low => "tickets-chip--priority-low",
            TicketPriority.Medium => "tickets-chip--priority-medium",
            TicketPriority.High => "tickets-chip--priority-high",
            TicketPriority.Critical => "tickets-chip--priority-critical",
            _ => string.Empty
        };

    private static string BuildSecondaryLine(TicketListItemResponse item)
    {
        if (item.DueAt is not null)
        {
            var prefix = item.IsOverdue ? "Overdue since" : "Due";
            return $"{prefix} {FormatCompactDateTime(item.DueAt.Value)}";
        }

        return $"Created {FormatCompactDateTime(item.CreatedAt)}";
    }

    private static string FormatDate(DateTimeOffset value)
        => value.ToLocalTime().ToString("dd MMM yyyy");

    private static string FormatTime(DateTimeOffset value)
        => value.ToLocalTime().ToString("HH:mm");

    private static string FormatCompactDateTime(DateTimeOffset value)
        => value.ToLocalTime().ToString("dd MMM yyyy, HH:mm");

    private async Task LoadAndSyncUrlAsync(TicketQueryParams query)
    {
        SyncUrl(query);
        await FeatureState.LoadAsync(query);
    }

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

            if (parsed.Length > 0)
            {
                statuses = parsed;
            }
        }

        TicketPriority[]? priorities = null;
        if (qs.TryGetValue("priorities", out var pv) && pv.Count > 0)
        {
            var parsed = pv
                .Where(s => Enum.TryParse<TicketPriority>(s, out _))
                .Select(s => Enum.Parse<TicketPriority>(s!))
                .ToArray();

            if (parsed.Length > 0)
            {
                priorities = parsed;
            }
        }

        string? assignee = qs.TryGetValue("assignee", out var av) ? (string?)av : null;

        return new TicketQueryParams(page, pageSize, sortField, sortDir, q, statuses, priorities, assignee);
    }

    private void SyncUrl(TicketQueryParams query)
    {
        var defaults = TicketQueryParams.Default;
        var qs = new List<KeyValuePair<string, string?>>();

        if (query.Page != defaults.Page)
        {
            qs.Add(new("page", query.Page.ToString()));
        }

        if (query.PageSize != defaults.PageSize)
        {
            qs.Add(new("pageSize", query.PageSize.ToString()));
        }

        if (query.SortField != defaults.SortField)
        {
            qs.Add(new("sort", query.SortField.ToString()));
        }

        if (query.SortDirection != defaults.SortDirection)
        {
            qs.Add(new("dir", query.SortDirection.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            qs.Add(new("q", query.Q));
        }

        if (query.Statuses is { Length: > 0 })
        {
            foreach (var status in query.Statuses)
            {
                qs.Add(new("statuses", status.ToString()));
            }
        }

        if (query.Priorities is { Length: > 0 })
        {
            foreach (var priority in query.Priorities)
            {
                qs.Add(new("priorities", priority.ToString()));
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Assignee))
        {
            qs.Add(new("assignee", query.Assignee));
        }

        var url = QueryHelpers.AddQueryString("/tickets", qs);
        Nav.NavigateTo(url, forceLoad: false, replace: true);
    }

    private void SyncFilterFieldsFromQuery(TicketQueryParams query)
    {
        _filterQ = query.Q;
        _filterStatuses = query.Statuses ?? [];
        _filterPriorities = query.Priorities ?? [];
        _filterAssignee = query.Assignee;
    }

    private void HandleStateChanged()
        => InvokeAsync(StateHasChanged);
}
