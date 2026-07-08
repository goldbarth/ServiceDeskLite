namespace ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;

/// <summary>
/// One knowledge-base document loaded from the corpus: a stable id (the file
/// slug), display metadata from the front matter, and the raw markdown body.
/// Articles are corpus content, not a domain aggregate — they carry no invariants
/// and are never mutated at runtime; the worker derives embeddings from them the
/// same way it derives ticket embeddings from tickets.
/// </summary>
public sealed record KnowledgeArticle(string Id, string Title, string Source, string Body);

/// <summary>
/// A single embeddable passage of an article: one section under a heading.
/// Chunking at section granularity keeps each vector focused and lets an answer
/// cite the exact heading it drew from, not just the whole document.
/// </summary>
public sealed record KnowledgeSection(int Ordinal, string Heading, string Content);
