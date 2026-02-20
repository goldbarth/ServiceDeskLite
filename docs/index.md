# ServiceDeskLite

ServiceDeskLite is a **.NET 10 Clean Architecture showcase** demonstrating:

- Strict inward dependency rules
- Explicit domain workflow handling (Ticket status transitions)
- Result-based error strategy (RFC 9457 ProblemDetails)
- Deterministic paging & sorting
- Provider-agnostic persistence (SQLite / InMemory)
- End-to-End testing across providers

---

## Architecture Deep Dive

- [Architecture Overview](architecture/overview.md)
- [Domain](architecture/domain.md)
- [Application](architecture/application.md)
- [API](architecture/api.md)
- [Infrastructure (SQLite)](architecture/infrastructure-sqlite.md)
- [Infrastructure (InMemory)](architecture/infrastructure-inmemory.md)
- [Web Layer](architecture/web.md)
