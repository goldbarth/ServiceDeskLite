using ServiceDeskLite.Application.Tickets.Shared;

namespace ServiceDeskLite.Application.Tickets.SearchTickets;

public sealed record SearchTickesResult(PagedResult<TicketListItemDto> Page);
