namespace ServiceDeskLite.Contracts.V1.Tickets;

public sealed record TicketListItemResponse(
    Guid Id,
    string Title,
    string Priority,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt
    );

