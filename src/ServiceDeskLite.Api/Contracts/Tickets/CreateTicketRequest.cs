using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Contracts.Tickets;

public sealed record CreateTicketRequest(
    string Title,
    string Description,
    TicketPriority Priority,
    DateTimeOffset? DueAt
    );
