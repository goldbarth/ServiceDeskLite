using ServiceDeskLite.Application.Abstractions.Search;

namespace ServiceDeskLite.Infrastructure.InMemory.Search;

/// <summary>
/// Semantic search needs pgvector; the InMemory provider reports it as
/// unavailable so the assistant tool can tell the model honestly instead of
/// returning a misleading empty result.
/// </summary>
public sealed class UnavailableTicketSimilaritySearch : ITicketSimilaritySearch
{
    public Task<TicketSimilaritySearchResult> SearchAsync(string query, int limit, CancellationToken ct) =>
        Task.FromResult(TicketSimilaritySearchResult.Unavailable);
}
