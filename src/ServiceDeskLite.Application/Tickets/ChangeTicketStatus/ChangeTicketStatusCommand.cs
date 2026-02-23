using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.ChangeTicketStatus;

public sealed record ChangeTicketStatusCommand(TicketId Id, TicketStatus NewStatus);
