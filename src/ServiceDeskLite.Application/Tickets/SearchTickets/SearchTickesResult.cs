using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.SearchTickets;

public sealed record SearchTickesResult(PagedResult<Ticket> Page);
