namespace ServiceDeskLite.Application.Abstractions.Search;

/// <summary>
/// Semantic search over the knowledge-base corpus (articles, FAQ, internal docs).
/// Mirrors <see cref="ITicketSimilaritySearch"/>: the Postgres provider embeds the
/// query and ranks chunks by cosine distance; providers without embeddings report
/// <see cref="KnowledgeSearchResult.IsAvailable"/> = false rather than a fake empty
/// result, so the assistant can tell the model honestly instead of citing sources
/// that were never retrieved.
/// </summary>
public interface IKnowledgeBaseSearch
{
    Task<KnowledgeSearchResult> SearchAsync(string query, int limit, CancellationToken ct);
}

/// <summary>
/// One retrieved knowledge-base passage. <see cref="ArticleId"/> and
/// <see cref="Heading"/> together identify the section so an answer can cite the
/// exact source; <see cref="Snippet"/> is the passage text the model grounds on.
/// </summary>
public sealed record KnowledgeMatch(
    string ArticleId,
    string Title,
    string Source,
    string Heading,
    string Snippet,
    double Similarity);

public sealed record KnowledgeSearchResult(
    bool IsAvailable,
    IReadOnlyList<KnowledgeMatch> Matches)
{
    public static KnowledgeSearchResult Unavailable { get; } = new(false, []);
}
