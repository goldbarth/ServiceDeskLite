# ADR 0005: Strongly-Typed Domain Identifiers

## Status

Accepted

## Context and Problem Statement

Every aggregate needs an identifier. The simplest choice is `Guid` directly
as the ID property type. The question is whether a raw `Guid` is precise
enough — or whether it leaves room for errors that the compiler could
otherwise prevent.

In a system with multiple aggregates, raw `Guid` parameters are
interchangeable. A method that expects a ticket ID will silently accept
a user ID or an attachment ID of the same `Guid` type. The mistake compiles,
passes static analysis, and only surfaces at runtime.

## Decision Drivers

- Passing a wrong ID to a method should be a compile error, not a runtime
  failure.
- The domain model should be self-documenting: `TicketId` communicates
  intent that `Guid` does not.
- ID generation must be owned by the domain layer, not by the database
  or the caller.
- The infrastructure layer must be able to map the type to a database
  column without special EF Core configuration ceremony.

## Considered Options

### Option A — `readonly record struct` wrapping `Guid` (Selected)

```csharp
public readonly record struct TicketId(Guid Value)
{
    public static TicketId New() => new(Guid.NewGuid());
}
```

A distinct type per aggregate root. The compiler rejects cross-aggregate
ID confusion. `TicketId.New()` encapsulates the creation strategy.
A value converter bridges the type to the database column.

### Option B — Raw `Guid` everywhere

No wrapper type. Simple and zero boilerplate, but all `Guid` values in
the system are interchangeable at the type level. Refactors that introduce
a second aggregate become risky — there is no compiler signal when an
ID from the wrong aggregate is passed.

### Option C — `string`-based identifier

A flexible approach used in some document-oriented systems, but gives up
the structural guarantees of a fixed-size value type, complicates sorting
and indexing, and shifts format validation to runtime.

## Decision Outcome

Chosen option: **Option A — `readonly record struct` wrapping `Guid`**.

### Why this trade-off makes sense for this project

- **`readonly record struct` is the right fit for a value identity.**
  It is immutable, stack-allocatable, has structural equality by default,
  and serialises cleanly. No heap allocation overhead compared to a class
  wrapper.
- **`TicketId.New()` makes ID ownership explicit.** The creation strategy
  (currently `Guid.NewGuid()`) is encapsulated in the domain type. Switching
  to UUIDv7 or another strategy in the future is a one-line change in one
  place, not a search across all callers.
- **The HTTP boundary stays clean.** The API endpoint receives a raw `Guid`
  from the route (`/{id:guid}`) and converts it to `new TicketId(id)` at
  the boundary. Inside the application and domain layers, only `TicketId`
  appears — `Guid` never leaks inward.
- **`ValueGeneratedNever()` enforces the domain-ownership rule in EF Core.**
  The database never generates an ID. The configuration makes this explicit
  rather than relying on convention.

### Layer boundaries for ID types

| Layer          | Type used  | Note                                      |
|----------------|------------|-------------------------------------------|
| Domain         | `TicketId` | Authoritative type                        |
| Application    | `TicketId` | Handlers and DTOs use the domain type     |
| Infrastructure | `TicketId` | Mapped to `Guid` via `TicketIdConverter`  |
| API (HTTP)     | `Guid`     | Route constraint `{id:guid}`, converted at entry point |
| Contracts      | `Guid`     | Response DTOs expose `Guid` for JSON serialisation     |

## Consequences

### Positive Consequences

- Cross-aggregate ID confusion is a compile error from the moment a second
  aggregate is introduced.
- The domain type carries its own creation semantics — callers never call
  `Guid.NewGuid()` directly.
- Value equality and `ToString()` work correctly without any manual
  implementation.

### Negative Consequences

- Each new aggregate requires a new ID type, a new value converter, and
  EF Core configuration. This is low cost per type but must be remembered.
- JSON serialisation of `TicketId` in responses would expose
  `{ "value": "..." }` rather than a plain string. This is avoided by
  using `Guid` in the Contracts layer, but that requires an explicit
  conversion at the mapping boundary.

## Re-evaluation Triggers

Revisit when:

1. The project adopts a source-generator–based strongly-typed ID library
   (e.g. `StronglyTypedId`) — the hand-rolled struct can be replaced with
   a generated equivalent without changing any consumers.
2. UUIDv7 or another time-ordered ID strategy is needed for natural
   sort order — change `TicketId.New()` only.

## Related

- ADR 0006 – EF Core + SQLite (value converter, `ValueGeneratedNever`)
- `src/ServiceDeskLite.Domain/Tickets/TicketId.cs`
- `src/ServiceDeskLite.Infrastructure/Persistence/Configurations/TicketIdConverter.cs`
- `src/ServiceDeskLite.Infrastructure/Persistence/Configurations/TicketConfiguration.cs`
- `src/ServiceDeskLite.Api/Endpoints/TicketsEndpoints.cs` — `new TicketId(id)` at HTTP boundary
