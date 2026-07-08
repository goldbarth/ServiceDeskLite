using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Assistant;

/// <summary>
/// Knowledge-base RAG degradation (issue #156). Semantic KB search needs the Voyage
/// embedding provider; the test host configures no Voyage key, so on both persistence
/// providers the search must report unavailable and return no matches rather than
/// fabricate sources. On Postgres this also proves the KnowledgeChunks schema exists
/// (migration applied) so the search path runs without error before gating out.
/// </summary>
public sealed class AssistantKnowledgeBaseTests
{
    [Theory]
    [ProviderMatrix]
    public async Task Search_degrades_honestly_without_voyage_key(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        using var scope = host.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<IKnowledgeBaseSearch>();

        var result = await search.SearchAsync("how do I reset a locked account?", 4, CancellationToken.None);

        result.IsAvailable.Should().BeFalse("no Voyage key is configured in the test host");
        result.Matches.Should().BeEmpty("an unavailable search must never invent sources");
    }
}
