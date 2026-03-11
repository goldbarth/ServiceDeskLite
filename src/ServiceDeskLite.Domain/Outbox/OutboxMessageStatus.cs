namespace ServiceDeskLite.Domain.Outbox;

/// <summary>
/// Lifecycle state of an outbox message.
/// A message starts as <see cref="Pending"/> and transitions to
/// <see cref="Dispatched"/> once a background processor has published it.
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>Written but not yet picked up by the dispatcher.</summary>
    Pending,

    /// <summary>Successfully published to an external consumer.</summary>
    Dispatched,
}