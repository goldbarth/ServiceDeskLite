using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Mapping.Tickets;

internal static class SearchTicketsMapping
{
    public static Paging ToPaging(this SearchTicketsRequest request)
        => new(request.Page, request.PageSize);

    public static SortSpec? ToSort(this SearchTicketsRequest request)
    {
        if (request.SortField is null && request.SortDirection is null)
            return null;

        return new SortSpec(
            Field: request.SortField?.ToApplication() ?? SortSpec.Default.Field,
            Direction: request.SortDirection?.ToApplication() ?? SortSpec.Default.Direction);
    }

    public static PagedResponse<TicketListItemResponse> ToPagedResponse(this PagedResult<Ticket> page)
    {
        var items = page.Items
            .Select(t => t.ToListItemResponse())
            .ToList();

        return new PagedResponse<TicketListItemResponse>(
            Items: items,
            Page: page.Paging.Page,
            PageSize: page.Paging.PageSize,
            TotalCount: page.TotalCount);
    }
}
