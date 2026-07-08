namespace ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;

/// <summary>
/// Source of knowledge-base articles for the embedding worker. Abstracted so the
/// corpus can come from files today and another store later without touching the
/// worker, and so tests can supply a fixed set instead of hitting the filesystem.
/// </summary>
public interface IKnowledgeCorpus
{
    IReadOnlyList<KnowledgeArticle> Load();
}
