using ServiceDeskLite.Application.Tickets.Shared;

namespace ServiceDeskLite.Application.Tickets.SearchTickets;

public sealed record SearchTicketsQuery(
    TicketSearchCriteria? Criteria,
    Paging? Paging = null,
    SortSpec? Sort = null);
