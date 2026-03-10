# ADR 0007: Swappable Persistence via Runtime Provider Switch

## Status

Accepted

## Context and Problem Statement

The application needs a persistence strategy that works in three distinct
contexts: production (durable, file-based), local development (fast startup,
no file system side effects), and automated tests (isolated, deterministic,
no shared state between test runs).

The straightforward answer is SQLite everywhere, or EF Core's built-in
InMemory provider for non-production contexts. The question is whether either
of those options is sufficient — or whether they obscure something the
architecture needs to prove.

## Decision Drivers

- Local development and tests must start without a database file or
  migration step.
- The InMemory implementation must honour the same unit-of-work commit
  boundary as the SQLite implementation — uncommitted adds must not be
  readable by concurrent requests.
- The port boundaries established in ADR 0001 must be verifiable: replacing
  the entire persistence stack should require no changes outside the
  infrastructure layer.
- The provider selection must not require a code change or a recompile —
  configuration only.

## Considered Options

### Option A — Hand-rolled InMemory provider with runtime config switch (Selected)

Two complete, interchangeable implementations of `ITicketRepository` and
`IUnitOfWork` exist: one backed by EF Core + SQLite, one backed by a
hand-rolled `ConcurrentDictionary` store. The active implementation is
selected at startup by reading `Persistence:Provider` from configuration.
The composition root (`InfrastructureComposition`) registers one set or the
other and throws on an unknown value — there is no silent fallback.

### Option B — EF Core InMemory provider for non-production contexts

Use `Microsoft.EntityFrameworkCore.InMemory` in place of SQLite for
development and tests. Less code to write — no separate infrastructure
project — but EF Core's InMemory provider does not honour transaction
semantics: writes are visible immediately without calling `SaveChanges`.
This makes it unsuitable for validating unit-of-work commit boundaries.

### Option C — SQLite only, always

Use a SQLite file in all contexts, applying migrations on startup. Simple,
but adds file system state to development and test runs, makes parallel
test execution harder, and increases local startup time.

## Decision Outcome

Chosen option: **Option A — Hand-rolled InMemory provider with runtime config switch**.

### Why this trade-off makes sense for this project

- **The swap proves the port boundaries work.** Running the application with
  `InMemory` or `Sqlite` exercises the same handler code, the same repository
  interface, and the same unit-of-work contract. If the boundaries are clean,
  the swap is invisible above the infrastructure layer. The fact that it works
  is ongoing proof that ADR 0001's dependency rules are holding.
- **EF Core InMemory is not a real UoW implementation.** It does not buffer
  writes until `SaveChanges` is called — adds are immediately visible. Using
  it would mean the InMemory and SQLite providers behave differently under
  the same test, which defeats the purpose of having two providers.
- **The hand-rolled store is intentionally minimal.** `InMemoryStore` is a
  `ConcurrentDictionary` with a snapshot read and a buffered-write flush.
  `InMemoryUnitOfWork` holds a `PendingAdds` list that is applied to the
  store only on `SaveChangesAsync`. This is enough to replicate the commit
  boundary without any ORM overhead.
- **Fail-fast on unknown provider.** `InfrastructureComposition` throws
  `InvalidOperationException` for any value other than `"InMemory"` or
  `"Postgres"`. A misconfigured deployment fails at startup with a clear
  message rather than silently falling back to an unintended state.

### DI lifetime rules

| Type                       | Lifetime  | Reason                                    |
|----------------------------|-----------|-------------------------------------------|
| `InMemoryStore`            | Singleton | Shared in-process state across requests   |
| `InMemoryUnitOfWork`       | Scoped    | Per-request pending-add buffer            |
| `InMemoryTicketRepository` | Scoped    | References the scoped UoW                 |
| `EfTicketRepository`       | Scoped    | DbContext is Scoped                       |
| `EfUnitOfWork`             | Scoped    | DbContext is Scoped                       |

### Configuration

```json lines
// appsettings.json (production)
{ "Persistence": { "Provider": "Postgres" } }

// appsettings.Development.json
{ "Persistence": { "Provider": "InMemory" } }
```

## Consequences

### Positive Consequences

- Development and test runs start instantly with no file system state.
- The InMemory and SQLite paths exercise the same application code —
  end-to-end tests cover both providers via the `[ProviderMatrix]` attribute.
- The commit-boundary behaviour is verifiable in tests
  (`CommitBoundaryTests`, `ReadIsolationTests`) because the InMemory
  provider deliberately does not expose uncommitted writes.
- The provider switch is a one-line config change; no code changes required.

### Negative Consequences

- Two infrastructure projects must be maintained in parallel. Filter/sort
  logic exists in both `EfTicketRepository` and `InMemoryTicketRepository`
  and must stay consistent.
- The hand-rolled store has no query optimisation. For large datasets in a
  non-production context, full-collection scans are the only option.

## Re-evaluation Triggers

Revisit when:

1. A second aggregate is introduced — the InMemory store needs to be extended
   for each new repository, increasing the maintenance overhead of keeping
   two implementations consistent.
2. Integration tests require a real database transaction (e.g. for
   serialisable isolation tests) — at that point SQLite is the right choice
   for all test contexts and the InMemory provider may be retired.

## Related

- ADR 0001 – Clean / Hexagonal Layered Architecture
- ADR 0008 – Unit-of-Work Pattern for Transactional Flush
- `src/ServiceDeskLite.Api/Composition/InfrastructureComposition.cs`
- `src/ServiceDeskLite.Infrastructure.InMemory/Persistence/InMemoryStore.cs`
- `src/ServiceDeskLite.Infrastructure.InMemory/Persistence/InMemoryUnitOfWork.cs`
- `src/ServiceDeskLite.Infrastructure.InMemory/DependencyInjection/InMemoryServiceCollectionExtensions.cs`
