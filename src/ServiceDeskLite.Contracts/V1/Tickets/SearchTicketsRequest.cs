using ServiceDeskLite.Contracts.V1.Common;

namespace ServiceDeskLite.Contracts.V1.Tickets;

public sealed record SearchTicketsRequest(
    int Page = 1,
    int PageSize = 25,
    TicketSortField? Sort = null,
    SortDirection? Direction = null
);
