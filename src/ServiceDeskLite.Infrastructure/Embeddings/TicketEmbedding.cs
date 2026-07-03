using Pgvector;

using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.Embeddings;

/// <summary>
/// Vector projection of a ticket's title + description, stored outside the
/// domain aggregate: an embedding is derived infrastructure state, not part
/// of the Ticket's invariants. ContentHash detects staleness after edits.
/// </summary>
public sealed class TicketEmbedding
{
    public TicketId TicketId { get; set; }

    public Vector Vector { get; set; } = null!;

    /// <summary>SHA-256 over "title\n\ndescription" — identifies the embedded content.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>Embedding model that produced the vector; guards against mixing models.</summary>
    public string Model { get; set; } = string.Empty;

    public DateTimeOffset EmbeddedAt { get; set; }
}
