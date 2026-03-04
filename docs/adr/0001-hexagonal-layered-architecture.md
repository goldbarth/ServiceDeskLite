# ADR 0001: Clean / Hexagonal Layered Architecture

## Status

Accepted

## Context and Problem Statement

ServiceDeskLite is a reference implementation of a ticket workflow system with
Kanban-style states and transitions. The central architectural question was:
how strictly should layer boundaries be enforced, given the project is
intentionally small?

A single-project CRUD design would have been the fastest path to working
software. But the goal of this project is to demonstrate that domain integrity
holds up when adapters, policies, and contracts evolve. That requires visible,
enforceable boundaries — not just naming conventions or comments.

Current solution shape:

- `Domain`: entities, invariants, workflow rules.
- `Application`: use cases and outbound port interfaces.
- `Infrastructure` and `Infrastructure.InMemory`: outbound adapter implementations.
- `Api`: inbound HTTP adapter using Minimal API.
- `Contracts`: boundary DTOs for the HTTP contract.
- `Web`: client application consuming API contracts.

## Decision Drivers

- Domain logic must not depend on HTTP, databases, or UI frameworks.
- Replacing the persistence provider (`SQLite` ↔ `InMemory`) must not require
  touching use-case code.
- Layer boundaries should be enforced by the compiler (project references),
  not only by convention or code reviews.
- Milestone 1 should not add framework layers that are not yet justified by
  real complexity.

## Considered Options

### Option A — Strict layering with explicit port interfaces (Selected)

Dependencies point strictly inward: `Domain ← Application ← Adapters`.
Repository and unit-of-work interfaces live in `Application`; concrete
implementations live in `Infrastructure`. The compiler enforces the dependency
direction through project references.

### Option B — Direct coupling from Application to persistence

Remove port interfaces and call persistence directly from use-case handlers.
This reduces files and short-term setup cost, but couples business logic to
the current infrastructure choice.

## Decision Outcome

Chosen option: **Option A — Strict layering with explicit port interfaces**.

### Why this trade-off makes sense for this project

- **Ports are used at volatility points, not everywhere.** Repository and
  unit-of-work abstractions exist because persistence is the most likely thing
  to change — and the working `InMemory` ↔ `SQLite` swap proves the boundary
  holds.
- **Multi-project ceremony is accepted intentionally.** A feature typically
  touches `Domain`, `Application`, and one adapter. That cost is accepted
  because the dependency direction is then enforced at compile time, not just
  in code reviews.
- **The over-engineering boundary is explicit for Milestone 1.** Keep
  structural boundaries (layers + ports). Do not add additional framework
  indirection until there is concrete pressure to do so.

## Consequences

### Positive Consequences

- Domain and use-case code is testable without a database or web host.
- Swapping or adding a persistence adapter has limited impact on other layers.
- Architectural intent is visible in project references, not only in
  documentation.

### Negative Consequences

- A feature typically touches multiple projects — higher ceremony per change.
- Mapping between domain, application, and contract models adds ongoing
  overhead.
- Some intentional duplication at boundaries to preserve isolation.
- Steeper onboarding compared to a flat CRUD project.

## Re-evaluation Triggers

Revisit this ADR when one or more of the following is true:

1. Cross-cutting concerns (logging, validation, auth) repeatedly require a
   reusable request pipeline — candidate: MediatR.
2. Endpoint or policy complexity outgrows Minimal API ergonomics —
   candidate: Controllers.
3. Layering overhead demonstrably slows delivery without measurable
   maintainability gain.

## Related

- ADR 0009 – Versioned HTTP Contracts in a dedicated `Contracts` project
- `docs/architecture/overview.md`
- `docs/architecture/contracts.md`
- `docs/structure/project-structure.md`
