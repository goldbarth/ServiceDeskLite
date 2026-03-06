using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.AssignTicket;

// AssigneeName = null means "unassign"
public sealed record AssignTicketCommand(TicketId Id, string? AssigneeName, string? Actor = null);
