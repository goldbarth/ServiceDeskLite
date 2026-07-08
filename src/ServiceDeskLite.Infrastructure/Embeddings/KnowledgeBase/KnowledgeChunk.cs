using Pgvector;

namespace ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;

/// <summary>
/// Vector projection of one article section, stored outside any domain aggregate:
/// like a ticket embedding, this is derived infrastructure state, not domain data.
/// <see cref="ContentHash"/> detects staleness after the corpus file is edited;
/// the id is deterministic (article id + ordinal) so re-embedding upserts in place.
/// </summary>
public sealed class KnowledgeChunk
{
    public Guid Id { get; set; }

    /// <summary>Article slug (source file name) — groups a document's sections and cites it.</summary>
    public string ArticleId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Heading { get; set; } = string.Empty;

    public int Ordinal { get; set; }

    /// <summary>Raw section text (no context prefix); returned as the citation snippet.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>SHA-256 over the embedded text (title + heading + content) — identifies staleness.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>Embedding model that produced the vector; guards against mixing models.</summary>
    public string Model { get; set; } = string.Empty;

    public Vector Vector { get; set; } = null!;

    public DateTimeOffset EmbeddedAt { get; set; }
}
