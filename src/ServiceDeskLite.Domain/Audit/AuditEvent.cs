using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Domain.Audit;

/// <summary>
/// Records a single auditable action on a ticket.
/// This is an append-only entity: once created, it is never modified.
/// </summary>
public sealed class AuditEvent
{
    public const int MaxEventTypeLength = 100;
    public const int MaxActorLength = 200;

    public AuditEventId Id { get; }
    public TicketId TicketId { get; }
    
    public string EventType { get; }

    public string? Actor { get; }

    public DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// Event-specific details as a JSON string. Serialization happens in the
    /// application layer — the domain only stores the pre-serialized value.
    /// </summary>
    public string Payload { get; }

    // Private constructor for EF Core materialization.
#pragma warning disable CS8618
    private AuditEvent() { }
#pragma warning restore CS8618

    public AuditEvent(
        AuditEventId id,
        TicketId ticketId,
        string eventType,
        string? actor,
        DateTimeOffset occurredAt,
        string payload)
    {
        Guard.NotNullOrWhiteSpace(eventType, nameof(eventType));
        Guard.MaxLength(eventType, MaxEventTypeLength, nameof(eventType));

        if (actor is not null)
            Guard.MaxLength(actor, MaxActorLength, nameof(actor));

        Guard.NotNullOrWhiteSpace(payload, nameof(payload));

        Id = id;
        TicketId = ticketId;
        EventType = eventType;
        Actor = actor;
        OccurredAt = occurredAt;
        Payload = payload;
    }
}
