namespace ServiceDeskLite.Api.Contracts.Tickets;

public sealed record TicketListItemResponse(
    Guid Id,
    string Title,
    string Priority,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt
    );

