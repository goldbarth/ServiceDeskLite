using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.UpdateTicket;

/// <summary>Partial update: null = keep current value. At least one field must be set.</summary>
public sealed record UpdateTicketCommand(
    TicketId Id,
    string? Title = null,
    string? Description = null,
    TicketPriority? Priority = null,
    DateTimeOffset? DueAt = null,
    string? Actor = null);
