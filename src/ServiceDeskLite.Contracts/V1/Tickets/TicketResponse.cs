namespace ServiceDeskLite.Contracts.V1.Tickets;

public sealed record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    string Priority,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt
    );
