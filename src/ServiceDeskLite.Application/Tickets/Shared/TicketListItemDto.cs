using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.Shared;

public record TicketListItemDto(
    TicketId Id,
    string Title,
    TicketStatus Status,
    TicketPriority Priority,
    TicketCategory Category,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    string? Assignee,
    IReadOnlyList<TicketStatus> AllowedTransitions,
    bool IsOverdue,
    string DisplayRef);
