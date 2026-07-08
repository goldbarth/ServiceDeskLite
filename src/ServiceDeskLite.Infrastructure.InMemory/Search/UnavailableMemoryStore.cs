using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Infrastructure.InMemory.Search;

/// <summary>
/// Long-term memory needs an embedding store (pgvector); the InMemory provider
/// reports it unavailable for both write and recall so the assistant tells the
/// model honestly instead of faking a stored or recalled fact.
/// </summary>
public sealed class UnavailableMemoryStore : IMemoryStore, IMemorySearch
{
    public Task<MemoryWriteResult> AddAsync(OwnerId owner, string content, string kind, CancellationToken ct)
        => Task.FromResult(MemoryWriteResult.Unavailable);

    public Task<MemorySearchResult> SearchAsync(OwnerId owner, string query, int limit, CancellationToken ct)
        => Task.FromResult(MemorySearchResult.Unavailable);
}
