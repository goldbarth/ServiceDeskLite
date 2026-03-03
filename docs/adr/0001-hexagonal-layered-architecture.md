# ADR 0001: Clean / Hexagonal Layered Architecture

## Status

Accepted

## Context and Problem Statement

ServiceDeskLite is a reference implementation of a ticket workflow system
(Kanban-style states and transitions). The architecture should demonstrate
senior-level boundary discipline under change, not just shortest-path delivery.

Current solution shape:

- `Domain`: entities, invariants, workflow rules.
- `Application`: use cases and outbound ports.
- `Infrastructure` and `Infrastructure.InMemory`: outbound adapters.
- `Api`: inbound HTTP adapter using Minimal API.
- `Contracts`: boundary DTOs for the HTTP contract.
- `Web`: client application consuming API contracts.

The key architecture problem is balancing clean boundaries with implementation
cost in a small project. A single-project CRUD design would be faster now, but
would hide whether the system can preserve domain integrity when adapters,
policies, and contracts evolve.

## Decision Drivers

- Domain behavior must stay independent from HTTP, ORM, and UI concerns.
- Persistence provider switching (`SQLite` ↔ `InMemory`) must not require
  use-case rewrites.
- API contracts must evolve separately from API-host composition concerns.
- Milestone 1 should avoid framework ceremony that is not yet justified by
  complexity.
- The repository should make architectural rules visible in project references,
  not only in comments/conventions.

## Considered Options

### Option A — Clean/Hexagonal layering with explicit ports (Selected)

Use strict inward dependencies (`Domain <- Application <- Adapters`).
Keep port interfaces in `Application`, implement adapters in `Infrastructure`.
Keep `Contracts` as a separate boundary project.
Use a thin Minimal API host in Milestone 1.
Defer MediatR until a clear pipeline need exists.

### Option B — Direct coupling from Application to persistence

Remove port boundaries and call persistence directly from use cases.
This reduces files and short-term ceremony but couples business flow to current
infrastructure choices.

### Option C — Controllers + MediatR from day one

Adopt full ASP.NET Controller + request pipeline stack immediately.
This gives established extension points early, but introduces additional
indirection and boilerplate before complexity requires it.

## Decision Outcome

Chosen option: **Option A — Clean/Hexagonal layering with explicit ports**.

### Why this is the right trade-off for this project

- **Port indirection is accepted intentionally**, even for small scope, because
  this repository is meant to prove boundary control under change. Ports are
  used at volatility points (persistence/unit-of-work boundaries), not as
  blanket abstraction.
- **Multi-project feature ceremony is accepted intentionally**. A feature often
  touches `Domain`, `Application`, and one adapter. That is a deliberate cost
  to enforce dependency direction at compile time and reduce erosion.
- **The over-engineering line is explicit for Milestone 1**: keep structural
  boundaries (layers + ports + contracts), but defer additional framework
  layers (MediatR pipelines, controller stack) until concrete pressure appears.

## Consequences

### Positive Consequences

- Domain and use cases stay testable without database or web-host runtime.
- Adapter replacement has limited blast radius.
- Contract lifecycle is decoupled from API-host internals.
- Architectural intent is discoverable through project structure and references.

### Negative Consequences

- Higher ceremony per feature across multiple projects.
- Ongoing mapping overhead between Domain/Application/Contracts models.
- Some intentional duplication at boundaries to preserve isolation.
- Steeper onboarding compared to a single-project CRUD baseline.

## Re-evaluation Triggers

Revisit this ADR when one or more of the following is true:

1. Cross-cutting concerns repeatedly require reusable request pipelines
   (candidate: MediatR).
2. Endpoint and policy complexity outgrows Minimal API ergonomics
   (candidate: Controllers).
3. Layering overhead repeatedly slows delivery without measurable
   maintainability benefit.

## Related

- `docs/architecture/overview.md`
- `docs/architecture/contracts.md`
- `docs/structure/project-structure.md`
