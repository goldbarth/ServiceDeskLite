using ServiceDeskLite.Application.Abstractions.Search;

namespace ServiceDeskLite.Infrastructure.InMemory.Search;

/// <summary>
/// Knowledge-base search needs pgvector; the InMemory provider reports it as
/// unavailable so the assistant tool can tell the model honestly instead of
/// citing sources that were never retrieved.
/// </summary>
public sealed class UnavailableKnowledgeBaseSearch : IKnowledgeBaseSearch
{
    public Task<KnowledgeSearchResult> SearchAsync(string query, int limit, CancellationToken ct) =>
        Task.FromResult(KnowledgeSearchResult.Unavailable);
}
