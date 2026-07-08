using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Pgvector;
using Pgvector.EntityFrameworkCore;

using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Infrastructure.Persistence;

namespace ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;

/// <summary>
/// Semantic search over knowledge-base chunks: embeds the query via Voyage, then
/// ranks by cosine distance in Postgres. Exact sequential scan — same reasoning as
/// ticket search. Gated on the Voyage key: without it the store reports unavailable
/// so the assistant never cites a source it did not actually retrieve.
/// </summary>
public sealed class PgVectorKnowledgeBaseSearch : IKnowledgeBaseSearch
{
    private readonly ServiceDeskLiteDbContext _db;
    private readonly IEmbeddingClient _embeddings;
    private readonly VoyageOptions _options;

    public PgVectorKnowledgeBaseSearch(
        ServiceDeskLiteDbContext db,
        IEmbeddingClient embeddings,
        IOptions<VoyageOptions> options)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<KnowledgeSearchResult> SearchAsync(string query, int limit, CancellationToken ct)
    {
        if (!_options.IsConfigured)
            return KnowledgeSearchResult.Unavailable;

        var queryVectors = await _embeddings.EmbedAsync([query], EmbeddingInputType.Query, ct);
        var queryVector = new Vector(queryVectors[0]);

        var rows = await _db.KnowledgeChunks
            .Select(c => new
            {
                c.ArticleId,
                c.Title,
                c.Source,
                c.Heading,
                c.Content,
                Distance = c.Vector.CosineDistance(queryVector),
            })
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToListAsync(ct);

        var matches = rows
            .Select(x => new KnowledgeMatch(
                x.ArticleId, x.Title, x.Source, x.Heading, x.Content,
                Similarity: 1.0 - x.Distance))
            .ToList();

        return new KnowledgeSearchResult(IsAvailable: true, matches);
    }
}
