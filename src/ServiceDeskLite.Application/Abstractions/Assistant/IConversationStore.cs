namespace ServiceDeskLite.Application.Abstractions.Assistant;

/// <summary>
/// Server-side persistence of assistant conversation transcripts, so a client
/// no longer resends the full history each turn. Implemented by both persistence
/// providers (Postgres + InMemory) to keep the swappable-persistence parity.
/// </summary>
/// <remarks>
/// A message's <see cref="ConversationMessage.Content"/> is an opaque, edge-owned
/// string: the message shape belongs to the Anthropic Messages API at the HTTP
/// edge, so the store treats it as a payload and stays ignorant of LLM types —
/// the inward-dependency rule holds.
/// </remarks>
public interface IConversationStore
{
    /// <summary>
    /// Returns the stored messages for <paramref name="id"/> owned by
    /// <paramref name="owner"/>, oldest first. An unknown id or a mismatched
    /// owner yields an empty list — never another owner's transcript.
    /// </summary>
    Task<IReadOnlyList<ConversationMessage>> GetAsync(
        ConversationId id, OwnerId owner, CancellationToken ct);

    /// <summary>Appends <paramref name="messages"/> to the conversation, creating it on first append.</summary>
    Task AppendAsync(
        ConversationId id, OwnerId owner, IReadOnlyList<ConversationMessage> messages, CancellationToken ct);
}

/// <param name="Sequence">Zero-based position within the conversation; defines replay order.</param>
/// <param name="Role">"user" or "assistant" — the Anthropic message role.</param>
/// <param name="Content">Opaque message payload, produced and consumed by the edge.</param>
public sealed record ConversationMessage(
    int Sequence,
    string Role,
    string Content,
    DateTimeOffset CreatedAt);
