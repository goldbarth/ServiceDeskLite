using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

/// <summary>
/// Conversation persistence backed by the singleton <see cref="InMemoryStore"/>,
/// so transcripts survive across requests without a database — the short-term
/// memory half works on the InMemory provider too.
/// </summary>
internal sealed class InMemoryConversationStore : IConversationStore
{
    private readonly InMemoryStore _store;

    public InMemoryConversationStore(InMemoryStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<IReadOnlyList<ConversationMessage>> GetAsync(
        ConversationId id, OwnerId owner, CancellationToken ct)
        => Task.FromResult(_store.GetConversation(id, owner));

    public Task AppendAsync(
        ConversationId id, OwnerId owner, IReadOnlyList<ConversationMessage> messages, CancellationToken ct)
    {
        _store.AppendConversation(id, owner, messages);
        return Task.CompletedTask;
    }
}
