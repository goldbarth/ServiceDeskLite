namespace ServiceDeskLite.Contracts.V1.Tickets;

public sealed record CreateTicketRequest(
    string Title,
    string Description,
    TicketPriority Priority,
    DateTimeOffset? DueAt
    );
