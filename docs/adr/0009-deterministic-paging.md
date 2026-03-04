# ADR 0009: Deterministic Paging and Tie-Breaking Sort

## Status

Accepted

## Context and Problem Statement

Paged queries are only reliable if the result order is stable across pages.
When two rows share the same value in the primary sort column (e.g. two
tickets created at the same millisecond, or five tickets with the same
priority), the database or in-memory engine is free to return them in any
order. On the next page request, that order may differ — causing items to
appear twice or be skipped entirely.

The question is: how do we guarantee a stable, gap-free pagination contract
without constraining which columns the caller can sort by?

## Decision Drivers

- A client paging through all tickets must see every ticket exactly once,
  regardless of which sort field is active.
- The sort order must be identical across both persistence providers (EF Core
  and InMemory) so that end-to-end tests can verify paging behaviour against
  either.
- The API must reject page and pageSize values outside the defined bounds
  before they reach the handler — invalid paging parameters must not cause
  silent data truncation or empty result sets.
- The default sort must be meaningful and documented.

## Considered Options

### Option A — Mandatory `Id` tiebreaker appended to every sort expression (Selected)

Every sort branch in both repository implementations appends
`.ThenBy(t => t.Id)` (EF Core) or `.ThenBy(t => t.Id.Value)` (InMemory)
after the primary sort clause. Since `TicketId` wraps a `Guid` and every
ticket has a unique ID, this guarantees a fully deterministic total order
for any result set, regardless of duplicate values in the primary column.

### Option B — Rely on the implicit database or engine ordering

Accept that rows with equal primary-sort values may come back in an
undefined order. Simple — no extra code — but breaks pagination for any
result set with duplicate sort values, which is common (many tickets can
share a status or priority).

### Option C — Force creation timestamp as the only sort field

Restrict the API to sorting by `CreatedAt` only. Sidesteps the tiebreaker
problem for most practical cases, but removes user-facing sort flexibility
and still breaks if two tickets are created within the same clock tick.

## Decision Outcome

Chosen option: **Option A — Mandatory `Id` tiebreaker on every sort expression**.

### Why this trade-off makes sense for this project

- **`TicketId` (a `Guid`) is unique by definition.** Appending it as a
  secondary sort key produces a total order over any result set — no two
  rows can compare equal after the tiebreaker is applied. Pages are
  therefore gap-free and overlap-free.
- **The tiebreaker is always ascending.** The primary sort direction can
  vary (Asc or Desc), but the `Id` tiebreaker is always `.ThenBy` — never
  `.ThenByDescending`. This keeps the secondary ordering stable and
  predictable regardless of the primary direction.
- **Paging bounds are enforced at the API endpoint before reaching the
  handler.** If `page < 1` or `pageSize` falls outside `[1, 200]`, the
  endpoint returns a `ValidationProblem` immediately. The handler never
  sees an out-of-range paging request. Constants are centralised in
  `PagingPolicy` to avoid magic numbers in both the endpoint and the
  repository layers.
- **The default sort is explicit and documented.**
  `SortSpec.Default = CreatedAt Desc + Id Asc` gives a meaningful
  out-of-the-box experience (newest tickets first) while remaining stable
  when multiple tickets share a timestamp.

### Paging policy constants

| Constant          | Value |
|-------------------|-------|
| `MinPage`         | 1     |
| `MinPageSize`     | 1     |
| `DefaultPageSize` | 25    |
| `MaxPageSize`     | 200   |

## Consequences

### Positive Consequences

- Clients can page through the full result set with any sort field without
  risk of gaps or duplicates.
- The behaviour is verified by `DeterministicPagingSortingTests` against
  both providers, including a case where three tickets share an identical
  `CreatedAt` timestamp.
- Out-of-range paging parameters produce a clear `400 ValidationProblem`
  rather than silently returning an empty or oversized page.

### Negative Consequences

- Every sort branch must explicitly append the tiebreaker — it cannot be
  applied automatically at a higher level without a more sophisticated query
  builder abstraction. A new sort field requires adding a new branch in both
  repository implementations with the tiebreaker included.
- The EF Core repository's catch-all `default` branch (unreachable in
  normal operation) does not include the `Id` tiebreaker, creating a minor
  inconsistency that would surface only if an unmapped `SortSpec` value were
  introduced without updating the sort switch.

## Re-evaluation Triggers

Revisit when:

1. A cursor-based pagination strategy is introduced — keyset pagination
   encodes the tiebreaker differently and does not use `Skip`/`Take`.
2. The sort field set grows significantly — at that point a query builder
   abstraction that automatically appends the tiebreaker becomes worthwhile.

## Related

- ADR 0007 – Swappable Persistence via Runtime Provider Switch
- `src/ServiceDeskLite.Application/Common/PagingPolicy.cs`
- `src/ServiceDeskLite.Application/Tickets/Shared/SortSpec.cs`
- `src/ServiceDeskLite.Infrastructure/Persistence/Repositories/EfTicketRepository.cs`
- `src/ServiceDeskLite.Infrastructure.InMemory/Persistence/InMemoryTicketRepository.cs`
- `src/ServiceDeskLite.Api/Endpoints/TicketsEndpoints.cs` — paging validation
- `tests/ServiceDeskLite.Tests.EndToEnd/Tickets/DeterministicPagingSortingTests.cs`
