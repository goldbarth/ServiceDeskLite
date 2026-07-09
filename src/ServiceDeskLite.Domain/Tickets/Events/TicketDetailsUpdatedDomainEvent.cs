using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets.Events;

/// <summary>Carries the new values of the fields that actually changed; null = unchanged.</summary>
public sealed record TicketDetailsUpdatedDomainEvent(
    TicketId TicketId,
    string? NewTitle,
    string? NewDescription,
    TicketPriority? NewPriority,
    DateTimeOffset? NewDueAt,
    TicketCategory? NewCategory = null) : IDomainEvent;
