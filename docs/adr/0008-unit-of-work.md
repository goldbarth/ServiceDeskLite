# ADR 0008: Unit-of-Work Pattern for Transactional Flush

## Status

Accepted

## Context and Problem Statement

The repository abstraction (`ITicketRepository`) provides `AddAsync` to
stage a new entity. The question is: when and how does that staged change
become durable and visible to other requests?

Two possible answers: the repository commits immediately on every write, or
writes are buffered until an explicit flush boundary is crossed. The choice
determines whether a use-case handler controls its own commit point — and
whether that contract can be tested and verified independently of the
infrastructure.

## Decision Drivers

- A use-case handler must be able to stage multiple writes and commit them
  as a single atomic operation.
- Uncommitted adds must not be visible to concurrent reads — not even within
  the same request scope.
- Both persistence implementations (EF Core and InMemory) must honour
  identical flush semantics so that end-to-end tests cover both without
  special-casing.
- `IUnitOfWork` belongs to the application layer: when to commit is a
  use-case concern, not an infrastructure detail.

## Considered Options

### Option A — Explicit `IUnitOfWork` with a separate `SaveChangesAsync` (Selected)

`IUnitOfWork` is a single-method interface in `Application.Abstractions.Persistence`.
Repositories buffer writes (EF Core change tracker / InMemory `PendingAdds`).
The handler calls `SaveChangesAsync` explicitly after all writes are staged.
Writes that are never committed are invisible to all subsequent reads.

### Option B — Implicit commit on each repository call

`AddAsync` commits immediately. No separate flush step, less interface
surface. But the handler loses the ability to stage multiple writes
atomically, and there is no clear signal in the code where persistence
actually occurs.

### Option C — Ambient commit on scope disposal

The DI scope disposes the unit of work at the end of each request, triggering
a flush automatically. Convenient but opaque: the commit point is invisible
in the handler, making it harder to reason about partial failure and
impossible to test without relying on scope disposal behaviour.

## Decision Outcome

Chosen option: **Option A — Explicit `IUnitOfWork` with a separate `SaveChangesAsync`**.

### Why this trade-off makes sense for this project

- **The commit point is visible in the handler.** `await _unitOfWork.SaveChangesAsync(ct)`
  is an explicit statement of intent: everything staged before this line is
  written together, or not at all. There is no implicit flush hiding in
  infrastructure plumbing.
- **`IUnitOfWork` lives in `Application`, not `Infrastructure`.** When a
  handler commits is a use-case decision. Placing the interface in the
  application layer enforces that — infrastructure provides the mechanism,
  application controls the when.
- **Both implementations honour the same flush boundary.** EF Core's change
  tracker defers writes until `SaveChanges`. The InMemory `PendingAdds` list
  does the same: `AddAsync` appends to the list, `SaveChangesAsync` applies
  it to the singleton store atomically. This means the commit boundary is
  verified by the same test suite against both providers.
- **The boundary is testable and tested.** `CommitBoundaryTests` and
  `ReadIsolationTests` run with `[ProviderMatrix]` against both providers
  and verify three guarantees:
  1. A committed ticket is visible in a subsequent scope.
  2. An uncommitted ticket is invisible in a subsequent scope.
  3. A staged but uncommitted ticket is invisible to reads within the same scope.

## Consequences

### Positive Consequences

- Partial failure within a handler is safe: if `SaveChangesAsync` is never
  reached (e.g. due to a domain validation failure earlier in the handler),
  staged writes are silently discarded with the scope.
- The flush boundary is a single, auditable call site in each handler.
- The InMemory and EF Core implementations are behaviourally equivalent,
  allowing end-to-end tests to run against both without conditional logic.

### Negative Consequences

- Every handler that writes must explicitly call `SaveChangesAsync`. Missing
  this call is a silent failure — the handler returns success but nothing
  is persisted.
- The separation of `ITicketRepository` and `IUnitOfWork` means two
  dependencies per handler instead of one, which adds minor boilerplate.

## Re-evaluation Triggers

Revisit when:

1. Multiple aggregates need to be written atomically within a single
   handler — at that point the scope of `SaveChangesAsync` needs to be
   defined explicitly (one UoW per handler vs. one per request).
2. Outbox or event-sourcing patterns are introduced, where the flush
   boundary must also publish domain events reliably.

## Related

- ADR 0007 – Swappable Persistence via Runtime Provider Switch
- `src/ServiceDeskLite.Application/Abstractions/Persistence/IUnitOfWork.cs`
- `src/ServiceDeskLite.Infrastructure/Persistence/UnitOfWork/EfUnitOfWork.cs`
- `src/ServiceDeskLite.Infrastructure.InMemory/Persistence/InMemoryUnitOfWork.cs`
- `tests/ServiceDeskLite.Tests.EndToEnd/Tickets/CommitBoundaryTests.cs`
- `tests/ServiceDeskLite.Tests.EndToEnd/Tickets/ReadIsolationTests.cs`
