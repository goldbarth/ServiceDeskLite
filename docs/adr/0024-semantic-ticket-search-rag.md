# ADR 0024: Semantic Ticket Search (RAG via pgvector + Voyage)

## Status

Accepted

## Context and Problem Statement

The AI intake assistant (ADR 0023) creates tickets from free text but has no
knowledge of existing tickets — users can file the same printer outage five
times and the assistant will happily create five tickets. The obvious fix is
retrieval: before creating a ticket, search existing ones *by meaning*
("Drucker reagiert nicht" should match "printout fails on floor 3"), which
keyword search does not deliver.

This requires three decisions:

1. **Where do embeddings come from?** Anthropic has no embeddings endpoint;
   an additional provider is unavoidable.
2. **Where are vectors stored and searched?** Dedicated vector DB vs. the
   existing PostgreSQL.
3. **When are tickets embedded?** Synchronously in the write path vs.
   asynchronously.

## Decision Drivers

- **Domain stays clean** — an embedding is derived infrastructure state, not
  part of the `Ticket` aggregate's invariants. The domain must not know that
  vectors exist (same inward-dependency reasoning as ADR 0023).
- **No new infrastructure for showcase scale** — a second datastore for a few
  hundred tickets is operational overhead without benefit.
- **Write path must not gain a network dependency** — ticket creation works
  today without any external call (the LLM sits at the edge, not in the
  command handler); an embedding API call must not change that.
- **Graceful degradation** — the app must run unchanged without a Voyage key
  and with the InMemory provider (Development default).

## Decision Outcome

**Embeddings: Voyage AI** (`voyage-3.5`, 1024 dims) — Anthropic's recommended
embedding provider. A minimal typed-HttpClient REST adapter
(`VoyageEmbeddingClient`), since no official .NET SDK exists.

**Storage/search: pgvector in the existing PostgreSQL** (image
`pgvector/pgvector:pg17`). Vectors live in a separate `TicketEmbeddings`
table (FK → `Tickets`, cascade delete) mapped in Infrastructure only; the
`Ticket` aggregate is untouched. Search is an exact sequential scan by cosine
distance — no HNSW/IVFFlat index, which would add tuning surface with zero
payoff at this data size.

**Indexing: asynchronous poll-based worker** (`TicketEmbeddingWorker`). One
mechanism covers every write path: new tickets, edited tickets (staleness
detected via content hash over title + description), seeded data, backfill,
and model upgrades (model name is stored per embedding). Search is thereby
*eventually consistent* — acceptable for duplicate detection, and the write
path keeps zero network dependencies.

**Retrieval surface: an assistant tool** (`find_similar_tickets`), not a REST
endpoint. The model decides when to search (RAG as agent tool); the system
prompt instructs it to check for duplicates before `create_ticket`. The
Application layer exposes only `ITicketSimilaritySearch`; the InMemory
provider registers an implementation that reports "unavailable", which the
tool relays honestly to the model instead of faking an empty result.

### Configuration

| Key | Default | Meaning |
|-----|---------|---------|
| `Voyage:ApiKey` | `placeholder-rag-disabled` | Sentinel disables worker + search gracefully |
| `Voyage:Model` | `voyage-3.5` | Embedding model, stored per vector |
| `Voyage:Dimensions` | `1024` | Validated at startup against the column width |

Unlike `Anthropic:ApiKey` (fail-fast — the assistant is a core feature), the
Voyage key defaults to the placeholder: RAG is an optional enhancement.

## Consequences

- **Positive:** duplicate detection grounded in existing tickets; no second
  datastore; domain and write path untouched; deployable without a Voyage key.
- **Negative:** a second AI provider (key, quota, failure mode); search lags
  ticket writes by up to one poll interval; sequential scan does not scale
  past ~100k tickets (revisit: HNSW index, then keyset-paged embedding
  queries).
- **Deliberately cut** (scale-appropriate, documented for the record):
  chunking (tickets are short — one vector per ticket), hybrid FTS+vector
  search, re-ranking, embedding of comments.
