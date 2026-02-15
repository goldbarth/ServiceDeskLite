using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.GetTicketById;

public sealed record GetTicketByIdQuery(TicketId Id);
