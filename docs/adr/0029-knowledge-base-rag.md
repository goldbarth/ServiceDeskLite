# ADR 0029: Knowledge-Base RAG with Cited Streaming Answers

## Status

Accepted

## Context and Problem Statement

Semantic ticket search (ADR 0024) lets the assistant ground itself in *existing
tickets* — enough to spot duplicates, but not to *answer* a "how do I…" question.
Users also ask for solutions ("how do I reset a locked account?", "VPN connects
but no traffic"), and the honest answer lives in help articles, FAQs, and internal
runbooks, not in the ticket table. Issue #156 turns the ticket-only retrieval into
a knowledge system the agent can cite.

This requires four decisions:

1. **Where does the corpus live?** A managed content store / domain aggregate vs.
   files in the repo.
2. **What granularity is embedded?** Whole articles vs. sections.
3. **How are answers grounded and shown?** Free-form text vs. explicit citations.
4. **How does it degrade** without a Voyage key or on the InMemory provider.

## Decision Drivers

- **Reuse the proven pipeline.** ADR 0024 already established Voyage + pgvector +
  a poll-based worker with honest degradation. A second retrieval feature should
  extend that pattern, not invent a parallel one.
- **Domain stays clean.** A knowledge article carries no invariants and is never
  mutated at runtime; like an embedding, it is content/derived state, not a domain
  aggregate. The domain must not learn that a corpus exists.
- **Honest grounding.** The distinguishing risk of RAG is a fabricated citation.
  Sources must be shown only when actually retrieved; unavailable search must say so.
- **Graceful degradation** unchanged: runs without a Voyage key and on InMemory.

## Decision Outcome

**Corpus: a `/KnowledgeBase` directory of markdown files** (articles, FAQ, internal
docs), one file per article, optional `title`/`source` front matter. The file slug
is the stable article id. Files are copied next to the API and read by
`FileKnowledgeCorpus`; a missing directory yields an empty corpus, not a boot
failure. No domain aggregate, no CRUD — the corpus is repo content, versioned with
the code. `IKnowledgeCorpus` abstracts the source so a managed store could replace
files later without touching the worker.

**Granularity: section chunks.** `KnowledgeChunker` splits each article on level-2
(`## `) headings; content before the first heading becomes an "Overview" chunk.
Each section is embedded separately, so retrieval and citations resolve to a
specific heading, not a whole document. Unlike tickets (short — one vector each,
ADR 0024), articles are long enough that chunking materially improves grounding.
Chunk ids are deterministic (`article id + ordinal`) so re-embedding upserts in
place.

**Storage/indexing: pgvector + a poll-based worker**, mirroring ticket embeddings.
`KnowledgeChunks` (mapped in Infrastructure only) holds the vectors;
`KnowledgeEmbeddingWorker` embeds new/stale chunks (content-hash or model mismatch)
in batches and **prunes chunks whose article or section left the corpus**, so
editing a markdown file and redeploying converges the index. Search is an exact
cosine scan — no HNSW/IVFFlat at this scale. Both workers share the Voyage client
and its enabled/disabled gate.

**Retrieval + citation: an assistant tool** (`search_knowledge_base`), not a REST
endpoint. The model decides when to consult the corpus; the system prompt directs
it to use the tool for how-to/policy questions and cite passages by title. The
Application layer exposes only `IKnowledgeBaseSearch`; the InMemory provider
registers an "unavailable" implementation. Retrieved passages are returned to the
model as the tool_result **and** surfaced to the client as a distinct `citation`
SSE event carrying structured sources (title, source, heading, snippet, similarity),
which the web client renders as a "Sources" card under the answer.

To carry citations without growing the per-tool return tuple again (it already held
four values after ADR 0028), tool results are now a `ToolResult` record; the
existing tuple-returning tools convert implicitly, so only the new signal was added.

### Honest degradation

`PgVectorKnowledgeBaseSearch` gates on the Voyage key exactly like ticket search;
without it (or on InMemory) the store returns `IsAvailable = false`. The tool then
tells the model the knowledge base could not be consulted and **emits no citations**
— it never fabricates a source. Empty results are reported as "nothing found", also
without citations. Verified across both persistence providers in `Tests.EndToEnd`.

### Configuration

| Key | Default | Meaning |
|-----|---------|---------|
| `KnowledgeBase:Path` | `KnowledgeBase` | Corpus directory, relative to the app base dir unless rooted |
| `Voyage:ApiKey` | `placeholder-rag-disabled` | Shared gate — disables worker + search gracefully |

## Consequences

- **Positive:** the assistant answers grounded in real documentation with visible,
  honest citations; corpus is versioned repo content; the ticket-embedding pattern
  is reused wholesale (worker, gating, degradation); domain and write path untouched.
- **Negative:** a second embedding worker and table; search lags corpus edits by up
  to one poll interval; sequential scan does not scale past a large corpus (revisit:
  HNSW index). Naive `## ` chunking ignores nested/code-fenced headings — fine for
  the curated corpus, revisit if articles grow complex.
- **Deliberately cut** (scale-appropriate, for the record): hybrid FTS + vector and
  metadata-weighted retrieval (ROADMAP item 5), re-ranking, hallucination/grounding
  scoring (ROADMAP item 6, issue #158), and any runtime authoring UI for articles.
