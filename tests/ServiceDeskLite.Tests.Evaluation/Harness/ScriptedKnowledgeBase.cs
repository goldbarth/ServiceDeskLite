using ServiceDeskLite.Application.Abstractions.Search;

namespace ServiceDeskLite.Tests.Evaluation.Harness;

/// <summary>
/// A knowledge base with fixed contents. Replaces the pgvector search so RAG scenarios need no
/// embedding provider, and so the passages an answer is graded against are known to the test.
/// </summary>
/// <remarks>
/// Defaults to unavailable, matching a deployment without a Voyage key. A scenario opts into
/// retrieval by seeding passages, which keeps the honest-degradation path the default one.
/// </remarks>
public sealed class ScriptedKnowledgeBase : IKnowledgeBaseSearch
{
    private readonly List<KnowledgeMatch> _matches = [];
    private bool _isAvailable;

    public string? LastQuery { get; private set; }

    public ScriptedKnowledgeBase Returns(params KnowledgeMatch[] matches)
    {
        _isAvailable = true;
        _matches.AddRange(matches);
        return this;
    }

    /// <summary>Available, but nothing matched — distinct from having no search at all.</summary>
    public ScriptedKnowledgeBase ReturnsNothing()
    {
        _isAvailable = true;
        return this;
    }

    public Task<KnowledgeSearchResult> SearchAsync(string query, int limit, CancellationToken ct)
    {
        LastQuery = query;

        return Task.FromResult(_isAvailable
            ? new KnowledgeSearchResult(true, _matches.Take(limit).ToList())
            : KnowledgeSearchResult.Unavailable);
    }

    public static KnowledgeMatch Passage(string heading, string snippet, double similarity = 0.82) =>
        new(
            ArticleId: "kb-001",
            Title: "Account access",
            Source: "articles/account-access.md",
            Heading: heading,
            Snippet: snippet,
            Similarity: similarity);
}
