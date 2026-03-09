namespace ServiceDeskLite.Contracts.V1.Tickets;

public sealed record TicketListItemResponse(
    Guid Id,
    string Title,
    TicketPriority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt);

