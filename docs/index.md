# ServiceDeskLite

A small service desk on .NET 10, built to put Clean Architecture into practice.
An AI assistant was added later, as an experiment.
The [README](https://github.com/goldbarth/ServiceDeskLite#readme) says how the two parts should be read.

The project is not developed further; `v1.9.0` is where it stands.

## What the architecture shows

- Strict inward dependency rules (Clean/Hexagonal Architecture)
- Explicit domain workflow handling (ticket status transitions, domain events)
- Result-based error strategy (RFC 9457 ProblemDetails)
- Audit trail with typed polymorphic payloads (ADR 0020)
- Outbox pattern stub for reliable event publication (ADR 0021)
- Dashboard KPI summary endpoint and UI
- Demo-grade API key authentication and config hardening (ADR 0022)
- Deterministic paging and sorting
- Provider-agnostic persistence (PostgreSQL / InMemory)
- Docker Compose setup with PostgreSQL
- End-to-end testing across both persistence providers

## What the assistant experiment adds

- Twelve tools over the command handlers, streamed over server-sent events (ADR 0023)
- Ticket and knowledge-base retrieval, with citations and a grounding check (ADR 0029, 0030, 0039)
- A guard pipeline every tool call passes (ADR 0035)
- A background worker behind a review guard (ADR 0037)
- Metrics, traces and the AI Insights page (ADR 0034, 0036)

---

## Architecture

- [Architecture Overview](architecture/overview.md)
- [Domain](architecture/domain.md)
- [Application](architecture/application.md)
- [Contracts](architecture/contracts.md)
- [API](architecture/api.md)
- [AI Assistant](architecture/assistant.md)
- [Infrastructure (PostgreSQL/EF Core)](architecture/infrastructure-postgres.md)
- [Infrastructure (InMemory)](architecture/infrastructure-inmemory.md)
- [Web Layer](architecture/web.md)

## Structure

- [Project Structure](structure/project-structure.md)
- [Solution Map](structure/solution-map.md)

## API

- [OpenAPI reference and Swagger UI](api/openapi.md)

## Testing

- [Testing Overview](testing/overview.md)
- [End-to-End](testing/e2e.md)
- [AI Assistant Manual Test Plan](testing/ai-assistant-manual-test-plan.md)

## Operations

- [Runbook](operations/runbook.md)
- [Commands](operations/commands.md)
- [CI](operations/ci.md)
- [Commit Conventions](operations/commit-conventions.md)

## Decisions

- [ADR Index](adr/index.md), all 42 decision records
