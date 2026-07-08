namespace ServiceDeskLite.Application.Abstractions.Assistant;

public readonly record struct ConversationId(Guid Value)
{
    public static ConversationId New() => new(Guid.CreateVersion7());
}
