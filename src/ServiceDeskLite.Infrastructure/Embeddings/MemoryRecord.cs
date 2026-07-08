using Pgvector;

using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Infrastructure.Embeddings;

/// <summary>
/// A long-term memory: a durable fact/preference plus the embedding used to
/// recall it. Unlike a ticket (embedding derived from a separate aggregate), a
/// memory is content and vector in one write-once row — no staleness tracking.
/// </summary>
public sealed class MemoryRecord
{
    public MemoryId Id { get; set; }

    public OwnerId Owner { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>Free-form category the model assigns: e.g. profile, preference, fact.</summary>
    public string Kind { get; set; } = string.Empty;

    public Vector Vector { get; set; } = null!;

    /// <summary>Embedding model that produced the vector; guards against mixing models.</summary>
    public string Model { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
