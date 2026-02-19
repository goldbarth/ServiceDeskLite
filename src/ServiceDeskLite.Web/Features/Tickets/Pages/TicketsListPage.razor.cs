using Microsoft.AspNetCore.Components;

using MudBlazor;

using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;

using SortDirection = ServiceDeskLite.Contracts.V1.Common.SortDirection;

namespace ServiceDeskLite.Web.Features.Tickets.Pages;

public partial class TicketsListPage
{
    [Inject] private NavigationManager Nav { get; set; } = default!;
    
    private int _page = 1;
    private int _pageSize = PagingPolicy.DefaultPageSize;

    private bool _isLoading;
    private PagedResponse<TicketListItemResponse>? _paged;
    
    private ApiError? _apiError;
    private Exception? _unexpectedError;

    private CancellationTokenSource? _cts;
    private long _loadSeq;
    
    private TicketSortField _sortField = TicketSortField.CreatedAt;
    private SortDirection _sortDirection = SortDirection.Desc;
    
    private object? ErrorForLoadable => _apiError;

    protected override async Task OnInitializedAsync()
        => await LoadAsync(_page);

    private async Task OnPageChangedAsync(int page)
    {
        _page = page;
        await LoadAsync(_page);
    }
    
    private string SortIcon(TicketSortField field)
    {
        if (_sortField != field)
            return string.Empty;

        return _sortDirection == SortDirection.Asc
            ? Icons.Material.Filled.ArrowUpward
            : Icons.Material.Filled.ArrowDownward;
    }
    
    private async Task LoadAsync(int page)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var seq = Interlocked.Increment(ref _loadSeq);

        _isLoading = true;
        _apiError = null;
        _unexpectedError = null;
        StateHasChanged();

        try
        {
            var req = new SearchTicketsRequest(
                Page: page,
                PageSize: _pageSize,
                SortField: _sortField,
                SortDirection: _sortDirection);

            var result = await TicketsApi.SearchAsync(req, _cts.Token);

            if (seq != _loadSeq)
                return; // ignore outdated result

            if (result.IsSuccess)
            {
                _paged = result.Value!;
                if (_paged.TotalPages > 0)
                    _page = Math.Min(_page, _paged.TotalPages);
            }
            else
            {
                _paged = null;
                _apiError = result.Error; 
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _paged = null;
            _unexpectedError = ex;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
    
    private async Task SortByAsync(TicketSortField field)
    {
        if (_sortField == field)
        {
            _sortDirection = _sortDirection == SortDirection.Asc
                ? SortDirection.Desc
                : SortDirection.Asc;
        }
        else
        {
            _sortField = field;
            _sortDirection = SortDirection.Asc;
        }

        _page = 1;
        await LoadAsync(_page);
    }
    
    private async Task OnPageSizeChangedAsync(int pageSize)
    {
        if (_pageSize == pageSize)
            return;

        _pageSize = pageSize;
        _page = 1;

        await LoadAsync(_page);
    }
    
    private void OnRowClick(TableRowClickEventArgs<TicketListItemResponse> args)
    {
        var id = args.Item!.Id;
        Nav.NavigateTo($"/tickets/{id}");
    }


    private Task ReloadAsync()
        => LoadAsync(_page);
    
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
