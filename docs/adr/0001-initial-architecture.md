# 0001. Clean / Hexagonal Layered Architecture

Date: 2026-03-03

## Status
Accepted

## Tags
architecture, layering, hexagonal, api, domain

## Context
ServiceDeskLite is a reference implementation of a ticket workflow system (Kanban-style states and transitions).
The architectural goal is to keep domain behavior stable while allowing adapter replacement (HTTP host, persistence provider, client) without rewriting use-case logic.

Current solution shape:

- `Domain`: entities, invariants, workflow rules.
- `Application`: use cases + port interfaces.
- `Infrastructure` and `Infrastructure.InMemory`: outbound adapters.
- `Api`: inbound HTTP adapter (Minimal API).
- `Contracts`: versioned boundary DTOs shared across HTTP boundary.
- `Web`: UI that consumes API contracts.

## Decision Drivers
1. Domain rules must remain independent from framework and data-access concerns.
2. We need provider switching (`SQLite` ↔ `InMemory`) for development/testing with no use-case rewrite.
3. We need explicit boundaries for API evolution (DTO versioning separated from host implementation).
4. The project must demonstrate architectural discipline **without** introducing unnecessary M1 framework machinery.

## Considered Options

### A) Clean/Hexagonal layered structure with explicit ports (selected)
- Inward dependency rule (`Domain <- Application <- Adapters`).
- Port interfaces in Application, implemented by Infrastructure adapters.
- Separate `Contracts` project for boundary DTOs.
- Thin Minimal API host for endpoint wiring and mapping.
- No MediatR in Milestone 1.

### B) Simpler direct-coupled application (no explicit ports)
- Application talks directly to EF Core/persistence implementation.
- Fewer files and less ceremony initially.
- Faster for very small scope, but harder provider replacement and weaker boundary enforcement.

### C) Full vertical slice with Controllers + MediatR from day one
- Standard pipeline tooling (behaviors, request/response abstractions).
- Better for larger teams/features immediately.
- Adds indirection and boilerplate before there is enough complexity to justify it.

## Decision
We adopt **Option A** as the architectural baseline for this repository.

### 1) Why accept port-interface indirection for a small project?
Because this repository is not optimized for shortest implementation path; it is optimized for demonstrating boundary control under change.
Ports are used where volatility is expected (persistence, unit-of-work boundaries), so use cases remain provider-agnostic.

### 2) Why accept multi-project feature ceremony?
Yes, a typical feature can touch `Domain`, `Application`, and one adapter project.
We accept this cost to make dependency direction explicit and enforceable at compile time.
This is the chosen trade: slower local feature edits in exchange for lower architectural erosion over time.

### 3) Where is the line between clean architecture and over-engineering here?
The line for Milestone 1 is:
- Keep structural boundaries (layers + ports + contracts).
- Avoid additional abstraction layers that do not currently reduce risk.

Concretely:
- **Included now:** ports, dedicated contracts project, minimal host, explicit error mapping.
- **Deferred:** MediatR pipelines, controller stack, extra cross-cutting frameworks.

## Consequences

### Positive
- Domain and use cases are testable without HTTP host or database runtime.
- Persistence adapter can be replaced with limited blast radius.
- HTTP contract versioning is decoupled from API host internals.
- Architecture rules are visible in project references, not only in conventions.

### Project-specific costs we explicitly accept
- More files/projects per feature (higher ceremony for small changes).
- Mapping overhead between Domain/Application/Contracts models.
- Some duplication is intentional to keep boundaries explicit.
- Onboarding takes longer than in a single-project CRUD structure.

### Re-evaluation triggers
Revisit this ADR if one of these becomes true:
1. Cross-cutting concerns require pipeline composition in many handlers (candidate: MediatR).
2. Endpoint surface and API policies outgrow Minimal API ergonomics (candidate: Controllers).
3. Feature throughput is repeatedly blocked by layering overhead without corresponding maintainability gains.

## Scope / Non-Goals
- This ADR sets the baseline architecture and dependency direction.
- It does not define every feature implementation pattern.
- It does not replace dedicated ADRs for persistence, error model, or API contract lifecycle.

## Related
- `docs/architecture/overview.md`
- `docs/architecture/contracts.md`
- `docs/structure/project-structure.md`
