namespace ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;

/// <summary>
/// Where the knowledge-base corpus lives and how often the worker reconciles it.
/// The corpus is static repo content (markdown copied next to the app), so a
/// relative <see cref="Path"/> resolves against the app base directory.
/// </summary>
public sealed class KnowledgeBaseOptions
{
    public const string SectionName = "KnowledgeBase";

    /// <summary>Directory of <c>*.md</c> articles, relative to the app base directory unless rooted.</summary>
    public string Path { get; init; } = "KnowledgeBase";
}
