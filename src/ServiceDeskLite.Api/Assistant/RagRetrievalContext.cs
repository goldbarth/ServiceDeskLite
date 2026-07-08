namespace ServiceDeskLite.Api.Assistant;

/// <summary>One knowledge-base passage retrieved this request, kept for grounding.</summary>
public sealed record RagPassage(string Title, string Heading, string Content);

/// <summary>
/// Per-request record of the knowledge-base passages retrieved by
/// <c>search_knowledge_base</c>, so <c>check_grounding</c> can score the model's draft
/// answer against the sources it actually saw — without the model re-passing them.
/// Scoped to the request: each chat turn starts with an empty context.
/// </summary>
public interface IRagRetrievalContext
{
    IReadOnlyList<RagPassage> Passages { get; }

    void Record(IEnumerable<RagPassage> passages);
}

public sealed class RagRetrievalContext : IRagRetrievalContext
{
    private readonly List<RagPassage> _passages = [];

    public IReadOnlyList<RagPassage> Passages => _passages;

    // Accumulates across multiple searches in one turn; a later grounding check then
    // scores against every source consulted, not only the most recent search.
    public void Record(IEnumerable<RagPassage> passages) => _passages.AddRange(passages);
}
