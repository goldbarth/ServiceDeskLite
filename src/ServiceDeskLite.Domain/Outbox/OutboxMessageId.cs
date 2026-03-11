namespace ServiceDeskLite.Domain.Outbox;

public readonly record struct OutboxMessageId(Guid Value)
{
    public static OutboxMessageId New() => new(Guid.CreateVersion7());
}