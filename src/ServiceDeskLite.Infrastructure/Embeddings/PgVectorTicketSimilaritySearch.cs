using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Pgvector;
using Pgvector.EntityFrameworkCore;

using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Infrastructure.Persistence;

namespace ServiceDeskLite.Infrastructure.Embeddings;

/// <summary>
/// Semantic search over ticket embeddings: embeds the query via Voyage, then
/// ranks by cosine distance in Postgres. Exact sequential scan — no HNSW/IVFFlat
/// index needed at this data size.
/// </summary>
public sealed class PgVectorTicketSimilaritySearch : ITicketSimilaritySearch
{
    private readonly ServiceDeskLiteDbContext _db;
    private readonly IEmbeddingClient _embeddings;
    private readonly VoyageOptions _options;

    public PgVectorTicketSimilaritySearch(
        ServiceDeskLiteDbContext db,
        IEmbeddingClient embeddings,
        IOptions<VoyageOptions> options)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<TicketSimilaritySearchResult> SearchAsync(string query, int limit, CancellationToken ct)
    {
        if (!_options.IsConfigured)
            return TicketSimilaritySearchResult.Unavailable;

        var queryVectors = await _embeddings.EmbedAsync([query], EmbeddingInputType.Query, ct);
        var queryVector = new Vector(queryVectors[0]);

        var rows = await _db.TicketEmbeddings
            .Join(
                _db.Tickets,
                e => e.TicketId,
                t => t.Id,
                (e, t) => new
                {
                    t.Id,
                    t.Title,
                    t.Status,
                    t.Priority,
                    Distance = e.Vector.CosineDistance(queryVector),
                })
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToListAsync(ct);

        var matches = rows
            .Select(x => new TicketSimilarityMatch(
                x.Id, x.Title, x.Status, x.Priority,
                Similarity: 1.0 - x.Distance))
            .ToList();

        return new TicketSimilaritySearchResult(IsAvailable: true, matches);
    }
}
