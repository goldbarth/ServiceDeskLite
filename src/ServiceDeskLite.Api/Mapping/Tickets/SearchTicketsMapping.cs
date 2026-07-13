using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;

namespace ServiceDeskLite.Api.Mapping.Tickets;

internal static class SearchTicketsMapping
{
    public static TicketSearchCriteria ToCriteria(this SearchTicketsRequest request)
        => new(
            Text: request.Q,
            Statuses: request.Statuses?.Select(s => s.ToDomain()).ToArray(),
            Priorities: request.Priorities?.Select(p => p.ToDomain()).ToArray(),
            AssigneeName: request.Assignee,
            Unassigned: request.Unassigned == true,
            Overdue: request.Overdue == true);

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

    public static PagedResponse<TicketListItemResponse> ToPagedResponse(this PagedResult<TicketListItemDto> page)
    {
        var items = page.Items
            .Select(dto => dto.ToListItemResponse())
            .ToList();

        return new PagedResponse<TicketListItemResponse>(
            Items: items,
            Page: page.Paging.Page,
            PageSize: page.Paging.PageSize,
            TotalCount: page.TotalCount);
    }
}
