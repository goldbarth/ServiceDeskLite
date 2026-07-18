# ADR 0042: Ticket Search Match Semantics (case-insensitive substring, both providers)

## Status

Accepted

## Context and Problem Statement

Free-text ticket search matched the query **case-sensitively**: `q=Remote` returned
hits, `q=remote` returned none. Users type lowercase, so search silently
underperformed. The behaviour is shared by the app-bar TopSearch, the tickets list,
and the command palette (#210), all of which route through `SearchTicketsHandler`
and one of the two `ITicketRepository` implementations.

The match was `Title.Contains(term) || Description.Contains(term)`:

- `EfTicketRepository.SearchAsync` (EF → PostgreSQL): `.Contains` translates to
  `LIKE`, which is case-sensitive under the default collation.
- `InMemoryTicketRepository.SearchAsync`: `string.Contains` is ordinal,
  case-sensitive.

There was also an **internal inconsistency**: the assignee-name filter already
matched case-insensitively on InMemory (`StringComparison.OrdinalIgnoreCase`) while
title/description did not. A bare `ToLower()` would fix the reported symptom but
leave the wider contract undecided, so this ADR settles the whole matching rule and
the provider-parity approach rather than patching one call site.

The open questions were: **case**, **accents/diacritics**, **partial vs. token**,
**provider parity**, and **ranking**.

## Decision Drivers

- **Baseline expectation.** Case-insensitive substring search is what users assume;
  the reported defect is purely casing.
- **Provider parity is load-bearing.** `Tests.EndToEnd` runs both persistence paths
  and they must agree. A rule that is cheap to keep identical on PostgreSQL and
  InMemory is worth more than a richer rule that drifts between them.
- **Scale-appropriate simplicity** (same ethos as ADR 0024). A few hundred tickets
  do not justify new extensions, indexes, or normalization machinery.
- **One rule, not per-field special cases.** The assignee filter and the
  title/description search should fold case the same way.

## Decision Outcome

**Case-insensitive, accent-sensitive, substring (`Contains`) matching, applied
identically on both providers. Ranking stays out of scope.**

- **Case: insensitive.**
  - PostgreSQL: `EF.Functions.ILike(column, pattern)` → `ILIKE`. The user term is
    escaped for LIKE metacharacters (`\`, `%`, `_`) and wrapped as `%term%`, so a
    literal `%` in the query matches a literal `%`, not "anything".
  - InMemory: `value.Contains(term, StringComparison.OrdinalIgnoreCase)`.

- **Accents: sensitive (normalization deferred).** `für` does **not** match `fur`.
  Accent-insensitive matching would require, on PostgreSQL, the `unaccent`
  extension plus an `IMMUTABLE` wrapper to stay indexable, and on InMemory a
  parallel Unicode `FormD` decomposition with combining-mark stripping — two
  divergent code paths to keep in parity for marginal benefit on the current
  (largely ASCII) corpus. `ILIKE` and `OrdinalIgnoreCase` are both accent-sensitive,
  so declining normalization is precisely what keeps the two providers identical.
  Recorded as future work if a diacritic-heavy corpus makes it worthwhile.

- **Partial: substring, unchanged.** `Contains` semantics are kept. Token, prefix,
  and full-text matching are ranking concerns, not correctness ones, and are out of
  scope here.

- **Assignee filter unified to the same rule.** The EF assignee filter moves from
  case-sensitive `Contains` to `ILike`; InMemory already used `OrdinalIgnoreCase`.
  Title/description and assignee now fold case the same way.

- **Ranking: out of scope.** Results stay ordered by the existing deterministic sort
  (ADR 0009). Relevance ranking is future work.

No schema change, migration, or collation change: the rule lives entirely at query
level, so it needs no new index and no data migration. Sequential `ILIKE` scans are
acceptable at this data size — the same trade-off ADR 0024 makes for vector search.

### Case-folding parity note

`ILIKE` folds case using the database locale; `OrdinalIgnoreCase` folds
culture-invariantly. For ASCII and common Latin text these agree; they can diverge
only on locale-specific edge cases (e.g. Turkish dotless *i*, Greek final sigma).
The showcase corpus does not exercise those, and both remain case-insensitive, which
is the contract this ADR promises.

## Consequences

- **Positive:** search matches regardless of case on both providers; the
  title/description and assignee filters share one rule; no new infrastructure,
  extension, index, or migration; `Tests.EndToEnd` can assert the same expectations
  against both persistence paths.
- **Negative:** accent-typed queries still miss accented content (documented, not
  silent); `ILIKE`/`OrdinalIgnoreCase` case-folding can differ at locale edges the
  corpus does not reach.
- **Deliberately deferred** (recorded for the record): accent/diacritic
  normalization (`unaccent` + InMemory `FormD`), token/prefix matching, and
  relevance ranking.
