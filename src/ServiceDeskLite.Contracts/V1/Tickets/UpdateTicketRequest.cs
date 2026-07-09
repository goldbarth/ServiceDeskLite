namespace ServiceDeskLite.Contracts.V1.Tickets;

/// <summary>Partial update: omitted/null fields keep their current value.</summary>
public sealed record UpdateTicketRequest(
    string? Title = null,
    string? Description = null,
    TicketPriority? Priority = null,
    DateTimeOffset? DueAt = null,
    TicketCategory? Category = null);
