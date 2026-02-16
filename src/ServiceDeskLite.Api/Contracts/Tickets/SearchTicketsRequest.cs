using ServiceDeskLite.Application.Tickets.Shared;

namespace ServiceDeskLite.Api.Contracts.Tickets;

public sealed record SearchTicketsRequest(
    int Page = 1,
    int PageSize = 25,
    TicketSortField? Sort = null,
    SortDirection? Direction = null
);
