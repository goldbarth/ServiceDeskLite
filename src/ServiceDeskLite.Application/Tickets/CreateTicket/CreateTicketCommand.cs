using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.CreateTicket;

public sealed record CreateTicketCommand(
    string Title,
    string Description,
    TicketPriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt = null);
