# ADR 0030: Hybrid Ticket Retrieval (RRF fusion of semantic + keyword + metadata)

## Status

Accepted

## Context and Problem Statement

`find_similar_tickets` retrieves candidate tickets for duplicate detection. Two
signals existed but were used in isolation: semantic search (ADR 0024, pgvector
cosine) and keyword search (`SearchTicketsHandler`, substring match). ADR 0028
wired them as *fallback* — keyword only when semantic was unavailable or empty.
That leaves quality on the table: semantic misses exact identifiers and short
tokens (error codes, hostnames) that keyword nails, and keyword misses paraphrases
that semantic nails. Issue #157 asks to *blend* the signals, constrain by metadata,
and score each result.

Decisions:

1. **How to merge two rankings** whose scores live on incompatible scales (cosine
   distance vs. substring recency)?
2. **Where does fusion live** — Infrastructure (SQL) or Application?
3. **What does "metadata / type" mean** given the ticket schema?
4. **How to keep provider parity and honest degradation.**

## Decision Drivers

- **No brittle score normalization** between fundamentally different signals.
- **Fusion is ranking logic, not persistence** — it should be provider-agnostic and
  unit-testable without a database.
- **Reuse existing ports** (`ITicketSimilaritySearch`, `ITicketRepository`) rather
  than a new SQL path.
- **Honest degradation** unchanged: works without a Voyage key and on InMemory.

## Decision Outcome

**Fusion: Reciprocal Rank Fusion (RRF).** Each signal contributes
`weight / (k + rank)` (k = 60, the original-paper default); scores are summed per
ticket. RRF uses only *rank position*, so it sidesteps normalizing cosine against
keyword scores — the property that makes it the right tool here. Both signals are
weighted equally (1.0); tunable, documented. The fused score is normalized to the
top match, giving a 0..1 relevance read as relative confidence.

**Placement: an Application service** (`HybridTicketSearch : IHybridTicketSearch`)
composing the two existing ports, with the fusion itself a pure static
(`ReciprocalRankFusion`). Registered once in `AddApplication`, so both persistence
providers share it and the semantic port swaps per provider. Unit-tested with fakes;
the ranking function is tested in isolation.

**Metadata / "type": priority + status.** The ticket aggregate has no "type"/category
field; priority is the categorical dimension the roadmap's "type" maps to. Filters
(`statuses`, `priorities`) constrain both signals: keyword filtering happens natively
in the repository query; semantic matches (whose port has no filter argument) are
post-filtered in the service. Priority additionally applies a light ranking nudge
(`× (1 + 0.15 · priorityFactor)`, Low = 0 … Critical = 1) — a tie-breaker, never
strong enough to float a weak textual match over a strong one.

**Retrieval surface unchanged:** still the `find_similar_tickets` tool, now backed by
the hybrid service and accepting optional `statuses`/`priorities`. Each result line
carries the fused relevance and the signals that matched it (`semantic`, `keyword`,
`semantic+keyword`). When semantic is unavailable, `HybridTicketSearchResult.
SemanticAvailable` is false, the blend is keyword-only, and the tool labels the
results as weaker evidence with null confidence — no faked vector score.

### Ranking function (for the record)

```
rrf(t)      = Σ_signals  weight_s / (k + rank_s(t))          # k = 60
boosted(t)  = rrf(t) · (1 + 0.15 · priorityFactor(t))
relevance(t)= boosted(t) / max_t boosted(t)                  # 0..1
```

## Consequences

- **Positive:** better grounding than either signal alone; a documented, testable
  ranking function; metadata filters; per-result relevance + provenance; no new SQL
  path or datastore; provider parity for free (fusion composes ports).
- **Negative:** two candidate fetches per query (over-fetched then trimmed); RRF
  weights and the priority factor are hand-tuned, not learned; semantic post-filtering
  can trim its candidate set below the requested limit before fusion (acceptable at
  this scale — the over-fetch absorbs it).
- **Deliberately cut:** learned/weighted ranking, a real full-text index (still
  substring `LIKE`), and cross-encoder re-ranking — future roadmap items.

## Updates to prior ADRs

- **ADR 0024** (semantic ticket search) and **ADR 0028** (self-critique / keyword
  *fallback*): the `find_similar_tickets` retrieval described there is superseded by
  this hybrid blend. Semantic-only search and the keyword-only *fallback* remain
  accurate as the degraded (no-Voyage / InMemory) path; the normal path now fuses
  both. The confidence signal from ADR 0028 is now the fused relevance.
