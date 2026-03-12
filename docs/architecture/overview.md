# Architecture Overview

ServiceDeskLite is structured as a strict layered system with inward-only dependencies.
The goal is a portfolio-grade reference for Clean Architecture with explicit domain rules and an explicit error strategy.

---

## System Context

<img src="../assets/diagrams/system-context.svg" alt="System Context" style="max-width:720px;width:100%;">

---

## Tech Summary

- .NET 10
- ASP.NET Core Minimal API + OpenAPI
- Blazor Interactive Server (MudBlazor, Bootstrap 5)
- EF Core 10
- PostgreSQL / Npgsql (production) + InMemory provider (dev/test)
- Docker Compose (PostgreSQL 17)
- xUnit + FluentAssertions + Microsoft.AspNetCore.Mvc.Testing + Testcontainers
- Serilog

---

## Solution Structure (High-Level)

- `Domain`: Aggregates, value objects, domain events, workflow rules, guards, domain exceptions
- `Application`: Use cases, `Result` / `Result<T>`, abstractions, validators, `IClock`
- `Contracts`: Versioned HTTP DTOs (V1) shared across boundaries
- `Infrastructure`: PostgreSQL/EF Core persistence + repositories
- `Infrastructure.InMemory`: In-memory persistence (dev/test)
- `API`: HTTP boundary, ProblemDetails mapping, exception handling, API key auth
- `Web`: Blazor UI + typed API client

---

## Dependency Rules

Dependencies must point strictly **inward**. No layer may reference anything from a layer that is outer to it.

<img src="../assets/diagrams/dependency-rules.svg" alt="Dependency Rules" style="max-width:720px;width:100%;">

Violations are blocking issues.

---

## Core Patterns

### Result-based execution
- Use-case handlers never throw for expected outcomes.
- All outcomes are represented as `Result` / `Result<T>`.
- Domain exceptions are caught in handlers and mapped to `ApplicationError`.

### RFC 9457 ProblemDetails at HTTP boundary
- API maps `ApplicationError` to RFC 9457 ProblemDetails.
- Error responses include structured extension fields (`code`, `errorType`, `traceId`, `meta`).

### Deterministic paging & sorting
- Paging is validated via a policy (min/max/default).
- Sorting is stable; ties use additional deterministic fields (`CreatedAt` + `Id`).

### Domain events + Audit trail
- Every aggregate mutation raises a domain event.
- Handlers consume events to write audit records and outbox messages atomically.

---

## Deep Dives

- [Domain](domain.md)
- [Application](application.md)
- [API](api.md)
- [Contracts](contracts.md)
- [Infrastructure (PostgreSQL/EF Core)](infrastructure-postgres.md)
- [Infrastructure (InMemory)](infrastructure-inmemory.md)
- [Web Layer](web.md)