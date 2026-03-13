# ServiceDeskLite

<p>
  <img src="https://img.shields.io/github/v/release/goldbarth/ServiceDeskLite"/>
  <a href="https://github.com/goldbarth/ServiceDeskLite/actions/workflows/ci.yml">
    <img src="https://github.com/goldbarth/ServiceDeskLite/actions/workflows/ci.yml/badge.svg" alt="CI" />
  </a>
  <a href="https://github.com/goldbarth/ServiceDeskLite/actions/workflows/docs.yml">
    <img src="https://github.com/goldbarth/ServiceDeskLite/actions/workflows/docs.yml/badge.svg" alt="Docs" />
  </a>
</p>

A structured .NET 10 backend reference application focused on Clean Architecture, explicit domain rules, and controlled dependencies.

## Core Focus

- Strict layering
- Explicit domain workflow rules
- Controlled dependencies
- Result-based error handling
- Testable application logic

The goal is not feature breadth, but structural clarity, explicit boundaries, and reviewable design decisions.

## Documentation

Full project documentation is available on GitHub Pages.

- [Documentation hub](https://goldbarth.github.io/ServiceDeskLite/)
- [Architecture overview](https://goldbarth.github.io/ServiceDeskLite/architecture/overview.html)
- [ADR index](https://goldbarth.github.io/ServiceDeskLite/adr/index.html)
- [OpenAPI reference and Swagger UI](https://goldbarth.github.io/ServiceDeskLite/api/openapi.html)
- [Testing overview](https://goldbarth.github.io/ServiceDeskLite/testing/overview.html)

The documentation site is the primary deep dive for architecture, ADRs, repository structure, and API details.

## Architecture

- Strict inward dependency flow across Domain, Application, Infrastructure, and API
- Result-based application flow with no exceptions crossing application boundaries
- RFC 9457 ProblemDetails as the HTTP error contract
- PostgreSQL and in-memory persistence behind the same application ports
- Explicit unit-of-work boundary for controlled writes
- End-to-end tests cover both persistence implementations

## Layer Structure

```text
┌─────────────────────────────────────┐
│              Web (Blazor)           │
├─────────────────────────────────────┤
│           API (Minimal API)         │
├───────────────────┬─────────────────┤
│  Infrastructure   │  Infra.InMemory │
├───────────────────┴─────────────────┤
│           Application               │
├─────────────────────────────────────┤
│              Domain                 │
└─────────────────────────────────────┘
```

Layer responsibilities:

- **Domain** — business rules, domain errors, events
- **Application** — use cases, validation, ports, unit-of-work coordination
- **Contracts** — versioned request/response models
- **Infrastructure / Infrastructure.InMemory** — persistence adapters behind application ports
- **API** — Minimal API endpoints and ProblemDetails mapping
- **Web** — optional UI for exercising the workflow

Dependency direction is strictly inward.

## Tech Stack

- .NET 10 / C#
- ASP.NET Core Minimal API
- Blazor
- EF Core
- PostgreSQL
- xUnit

## License

MIT. See [LICENSE](LICENSE).
