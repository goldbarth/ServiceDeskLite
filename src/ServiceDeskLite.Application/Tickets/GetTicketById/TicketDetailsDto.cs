using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.GetTicketById;

public record TicketDetailsDto(
    TicketId Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt);
