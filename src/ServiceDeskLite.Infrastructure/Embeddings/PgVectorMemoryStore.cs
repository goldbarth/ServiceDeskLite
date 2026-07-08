using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Pgvector;
using Pgvector.EntityFrameworkCore;

using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Infrastructure.Persistence;

namespace ServiceDeskLite.Infrastructure.Embeddings;

/// <summary>
/// Long-term memory over pgvector: writes embed the content synchronously (Voyage
/// Document) so a fact is recallable immediately in the same session; recall embeds
/// the query (Voyage Query) and ranks owner-scoped memories by cosine distance.
/// Implements both memory ports. Gated on the Voyage key — without it, memory
/// reports unavailable instead of storing/returning nothing silently.
/// </summary>
public sealed class PgVectorMemoryStore : IMemoryStore, IMemorySearch
{
    private readonly ServiceDeskLiteDbContext _db;
    private readonly IEmbeddingClient _embeddings;
    private readonly VoyageOptions _options;
    private readonly IClock _clock;

    public PgVectorMemoryStore(
        ServiceDeskLiteDbContext db,
        IEmbeddingClient embeddings,
        IOptions<VoyageOptions> options,
        IClock clock)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<MemoryWriteResult> AddAsync(OwnerId owner, string content, string kind, CancellationToken ct)
    {
        if (!_options.IsConfigured)
            return MemoryWriteResult.Unavailable;

        var vectors = await _embeddings.EmbedAsync([content], EmbeddingInputType.Document, ct);

        _db.Set<MemoryRecord>().Add(new MemoryRecord
        {
            Id = MemoryId.New(),
            Owner = owner,
            Content = content,
            Kind = kind,
            Vector = new Vector(vectors[0]),
            Model = _options.Model,
            CreatedAt = _clock.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return MemoryWriteResult.Stored;
    }

    public async Task<MemorySearchResult> SearchAsync(OwnerId owner, string query, int limit, CancellationToken ct)
    {
        if (!_options.IsConfigured)
            return MemorySearchResult.Unavailable;

        var queryVectors = await _embeddings.EmbedAsync([query], EmbeddingInputType.Query, ct);
        var queryVector = new Vector(queryVectors[0]);

        var rows = await _db.Set<MemoryRecord>()
            .Where(m => m.Owner == owner)
            .Select(m => new
            {
                m.Id,
                m.Content,
                m.Kind,
                Distance = m.Vector.CosineDistance(queryVector),
            })
            .OrderBy(x => x.Distance)
            .Take(limit)
            .ToListAsync(ct);

        var matches = rows
            .Select(x => new MemoryMatch(x.Id, x.Content, x.Kind, Similarity: 1.0 - x.Distance))
            .ToList();

        return new MemorySearchResult(IsAvailable: true, matches);
    }
}
