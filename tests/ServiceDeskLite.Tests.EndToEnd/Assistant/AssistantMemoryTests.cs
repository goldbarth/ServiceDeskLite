using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Assistant;

/// <summary>
/// Conversation persistence and long-term memory across both persistence providers.
/// Short-term conversation state must survive across scopes (i.e. across requests)
/// on both providers; long-term memory needs an embedding store, so without a Voyage
/// key it must degrade honestly (report unavailable) rather than fake a write or recall.
/// </summary>
public sealed class AssistantMemoryTests
{
    private static readonly OwnerId Owner = new(Guid.Parse("00000000-0000-0000-0000-0000000000aa"));

    [Theory]
    [ProviderMatrix]
    public async Task Conversation_survives_across_scopes_in_order(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);
        var conversation = ConversationId.New();
        var now = DateTimeOffset.UtcNow;

        using (var scope = host.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IConversationStore>();
            await store.AppendAsync(conversation, Owner,
            [
                new(0, "user", "my vpn is down", now),
                new(1, "assistant", "I opened a ticket.", now),
            ], CancellationToken.None);
        }

        // Second turn in a fresh scope: the client sent only the new message + id.
        using (var scope = host.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IConversationStore>();
            await store.AppendAsync(conversation, Owner,
            [
                new(2, "user", "any update?", now),
            ], CancellationToken.None);
        }

        using (var scope = host.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IConversationStore>();
            var messages = await store.GetAsync(conversation, Owner, CancellationToken.None);

            messages.Select(m => m.Content).Should().ContainInOrder(
                "my vpn is down", "I opened a ticket.", "any update?");
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Conversation_is_owner_scoped(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);
        var conversation = ConversationId.New();
        var otherOwner = new OwnerId(Guid.Parse("00000000-0000-0000-0000-0000000000bb"));

        using (var scope = host.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IConversationStore>();
            await store.AppendAsync(conversation, Owner,
                [new(0, "user", "private note", DateTimeOffset.UtcNow)], CancellationToken.None);
        }

        using (var scope = host.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IConversationStore>();
            var leaked = await store.GetAsync(conversation, otherOwner, CancellationToken.None);

            leaked.Should().BeEmpty("a conversation must never be readable by another owner");
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Memory_degrades_honestly_without_embeddings(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        using var scope = host.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMemoryStore>();
        var search = scope.ServiceProvider.GetRequiredService<IMemorySearch>();

        // No Voyage key is configured in the test host, so the embedding-backed store
        // must report unavailable on both providers instead of pretending success.
        var write = await store.AddAsync(Owner, "prefers email", "preference", CancellationToken.None);
        var recall = await search.SearchAsync(Owner, "contact preference", 5, CancellationToken.None);

        write.IsAvailable.Should().BeFalse();
        recall.IsAvailable.Should().BeFalse();
    }
}
