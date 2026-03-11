using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Outbox;

/// <summary>
/// Represents a domain event that must be reliably published to an external consumer.
///
/// SHOWCASE STUB — this entity demonstrates the Outbox pattern at the architectural
/// level. A production implementation would add retry counters, error columns, and a
/// dedicated background dispatcher. See ADR 0021 for the full rationale.
///
/// Lifecycle: <see cref="OutboxMessageStatus.Pending"/> → <see cref="OutboxMessageStatus.Dispatched"/>.
/// </summary>
public sealed class OutboxMessage
{
    public const int MaxEventTypeLength = 100;

    public OutboxMessageId Id { get; }

    /// <summary>
    /// The event type identifier.
    /// Convention: dot-separated lowercase, matching <c>AuditEventTypes</c> constants.
    /// Example: <c>"ticket.created"</c>.
    /// </summary>
    public string EventType { get; }

    /// <summary>Event-specific data serialised as a JSON string.</summary>
    public string Payload { get; }

    /// <summary>When the originating domain event occurred (UTC).</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>
    /// When this message was dispatched by the background processor.
    /// <c>null</c> means the message is still <see cref="OutboxMessageStatus.Pending"/>.
    /// </summary>
    public DateTimeOffset? DispatchedAt { get; private set; }

    public OutboxMessageStatus Status =>
        DispatchedAt.HasValue ? OutboxMessageStatus.Dispatched : OutboxMessageStatus.Pending;

    // Private constructor for EF Core materialization.
#pragma warning disable CS8618
    private OutboxMessage() { }
#pragma warning restore CS8618

    public OutboxMessage(
        OutboxMessageId id,
        string eventType,
        string payload,
        DateTimeOffset occurredAt)
    {
        Guard.NotNullOrWhiteSpace(eventType, nameof(eventType));
        Guard.MaxLength(eventType, MaxEventTypeLength, nameof(eventType));
        Guard.NotNullOrWhiteSpace(payload, nameof(payload));

        Id = id;
        EventType = eventType;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    /// <summary>
    /// Marks this message as dispatched.
    /// Called by the background dispatcher after successful publication.
    /// </summary>
    public void MarkDispatched(DateTimeOffset dispatchedAt)
        => DispatchedAt = dispatchedAt;
}