# ADR 0004: Minimal API Host; MediatR and Controller Stack Deferred

## Status

Accepted

## Context and Problem Statement

The API host needs a request routing and handler dispatch mechanism.
ASP.NET Core offers two established models: the traditional Controller stack
(attribute routing, action filters, model binding conventions) and the newer
Minimal API model (route delegates, direct DI injection, no controller base class).

A separate but related question: should a mediator library (MediatR) be
introduced from the start to decouple endpoints from handler types?

Both choices affect how much framework infrastructure exists on day one and
how much has to be justified by actual complexity.

## Decision Drivers

- Milestone 1 has a small, stable endpoint surface (four routes, one aggregate).
- Every layer of indirection should be justified by a concrete problem it solves,
  not by anticipated future needs.
- Handlers are already decoupled from HTTP concerns by the application layer
  boundary — additional dispatch indirection does not add isolation.
- The architecture should make it easy to add MediatR or Controllers later if
  the need becomes concrete.

## Considered Options

### Option A — Minimal API with handlers injected directly (Selected)

Endpoints are defined as static methods in a `RouteGroupBuilder` extension class.
Handler types (`CreateTicketHandler`, `GetTicketByIdHandler`, …) are injected
directly as endpoint parameters by the framework's DI-aware parameter binding.
No mediator interface sits between the endpoint and the handler.

### Option B — Minimal API with MediatR from day one

Same routing approach, but endpoints call `ISender.Send(command)` instead of
invoking handlers directly. Enables cross-cutting pipeline behaviours
(`IPipelineBehavior<,>`) without changing endpoint code, but introduces
a registration layer and an indirection cost before any pipeline behaviour
is actually needed.

### Option C — ASP.NET Core Controllers from day one

Attribute-routed controllers with constructor injection. Established pattern
with broad tooling support, but brings a base class, action filter pipeline,
and model binding conventions that add ceremony before the project has
the complexity to justify them.

## Decision Outcome

Chosen option: **Option A — Minimal API with handlers injected directly**.

### Why this trade-off makes sense for this project

- **The handler injection is already DI-decoupled.** The endpoint calls
  `handler.HandleAsync(command, ct)` — the same interface regardless of
  whether the handler is a direct type or behind a mediator. Swapping to
  MediatR later requires changing the endpoint call site, not the handler
  or its tests.
- **No pipeline behaviour is needed yet.** MediatR's main value is
  `IPipelineBehavior<,>` for cross-cutting concerns (logging, validation,
  auth). In Milestone 1 these are handled per-handler or at the middleware
  level. Adding a pipeline before there is something to put in it creates
  infrastructure debt, not capital.
- **Minimal API keeps the routing surface explicit.** All four endpoints
  are defined in one file (`TicketsEndpoints.cs`) with their OpenAPI metadata
  co-located. There is no magic from attribute scanning or convention-based
  route discovery.
- **The over-engineering boundary is the same as in ADR 0001.** Structural
  layers (Domain / Application / Adapters) are non-negotiable. Framework
  layers inside the adapter are deferred until complexity demands them.

## Consequences

### Positive Consequences

- Endpoint definitions are short and explicit: route, handler call,
  result mapping.
- Handler types are registered as plain scoped services — no mediator
  assembly scanning or marker interface required.
- Adding a new endpoint means adding one method; no controller class to
  locate or extend.

### Negative Consequences

- Every endpoint must explicitly inject each handler it needs. If an
  endpoint grows to orchestrate multiple handlers, the parameter list
  grows with it.
- Cross-cutting concerns (e.g. per-request validation logging) must be
  repeated per handler or pushed into middleware rather than expressed
  as a single pipeline behaviour.

## Re-evaluation Triggers

Revisit when:

1. A cross-cutting concern (auth, audit logging, retry) needs to apply
   to all handler calls uniformly without modifying each endpoint —
   candidate: MediatR `IPipelineBehavior<,>`.
2. The number of endpoints or the complexity of individual endpoints
   grows to the point where Minimal API's lack of filters and action
   conventions creates repetition — candidate: Controllers.

## Related

- ADR 0001 – Clean / Hexagonal Layered Architecture
- ADR 0002 – Result Pattern as Application Error Contract
- `src/ServiceDeskLite.Api/Endpoints/TicketsEndpoints.cs`
- `src/ServiceDeskLite.Api/Program.cs`
