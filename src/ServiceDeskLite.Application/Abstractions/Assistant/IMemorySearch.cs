namespace ServiceDeskLite.Application.Abstractions.Assistant;

/// <summary>
/// Semantic recall over an owner's long-term memories. Mirrors
/// <c>ITicketSimilaritySearch</c>: the Postgres provider embeds the query and
/// ranks by cosine distance; providers without embeddings report
/// <see cref="MemorySearchResult.IsAvailable"/> = false rather than a fake empty result.
/// </summary>
public interface IMemorySearch
{
    Task<MemorySearchResult> SearchAsync(OwnerId owner, string query, int limit, CancellationToken ct);
}

public sealed record MemoryMatch(
    MemoryId Id,
    string Content,
    string Kind,
    double Similarity);

public sealed record MemorySearchResult(
    bool IsAvailable,
    IReadOnlyList<MemoryMatch> Matches)
{
    public static MemorySearchResult Unavailable { get; } = new(false, []);
}
