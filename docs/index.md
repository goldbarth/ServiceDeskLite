# ServiceDeskLite

ServiceDeskLite is a **.NET 10 Clean Architecture showcase** demonstrating:

- Strict inward dependency rules (Clean/Hexagonal Architecture)
- Explicit domain workflow handling (Ticket status transitions, domain events)
- Result-based error strategy (RFC 9457 ProblemDetails)
- Audit trail with typed polymorphic payloads (ADR 0020)
- Outbox pattern stub for reliable event publication (ADR 0021)
- Dashboard KPI summary endpoint and UI
- Demo-grade API Key authentication and config hardening (ADR 0022)
- Deterministic paging & sorting
- Provider-agnostic persistence (PostgreSQL / InMemory)
- Docker Compose setup with PostgreSQL
- End-to-End testing across both persistence providers

---

## Architecture Deep Dive

- [Architecture Overview](architecture/overview.md)
- [Domain](architecture/domain.md)
- [Application](architecture/application.md)
- [API](architecture/api.md)
- [Infrastructure (PostgreSQL/EF Core)](architecture/infrastructure-postgres.md)
- [Infrastructure (InMemory)](architecture/infrastructure-inmemory.md)
- [Web Layer](architecture/web.md)
- [Contracts](architecture/contracts.md)