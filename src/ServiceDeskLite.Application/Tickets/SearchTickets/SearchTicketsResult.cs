using ServiceDeskLite.Application.Tickets.Shared;

namespace ServiceDeskLite.Application.Tickets.SearchTickets;

public sealed record SearchTicketsResult(PagedResult<TicketListItemDto> Page);
