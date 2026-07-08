using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Infrastructure.Persistence.Conversations;

/// <summary>
/// One persisted assistant message. A conversation is just its ordered messages
/// plus their owner — there is no aggregate with invariants, so no separate
/// parent table. <see cref="Content"/> is an edge-owned payload; the store never
/// interprets it.
/// </summary>
public sealed class ConversationMessageRecord
{
    public ConversationId ConversationId { get; set; }

    public int Sequence { get; set; }

    public OwnerId Owner { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
