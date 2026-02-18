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
        if (request.Sort is null && request.Direction is null)
            return null;

        return new SortSpec(
            Field: request.Sort?.ToApplication() ?? SortSpec.Default.Field,
            Direction: request.Direction?.ToApplication() ?? SortSpec.Default.Direction);
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
