namespace ServiceDeskLite.Contracts.V1.Tickets;

public sealed record TicketListItemResponse(
    Guid Id,
    string Title,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    string? Assignee);

