# ServiceDeskLite

<p>
  <a href="https://github.com/goldbarth/ServiceDeskLite/actions/workflows/ci.yml">
    <img src="https://github.com/goldbarth/ServiceDeskLite/actions/workflows/ci.yml/badge.svg" alt="CI" />
  </a>
  <a href="https://github.com/goldbarth/ServiceDeskLite/actions/workflows/docs.yml">
    <img src="https://github.com/goldbarth/ServiceDeskLite/actions/workflows/docs.yml/badge.svg" alt="Docs" />
  </a>
  <img src="https://img.shields.io/badge/.NET-10_LTS-512BD4?logo=dotnet&logoColor=white" alt=".NET 10 LTS" />
  <img src="https://img.shields.io/badge/License-MIT-2C3E50" alt="MIT" />
</p>

## Purpose

ServiceDeskLite is a structured backend application built to apply Clean Architecture principles in a modern .NET (LTS) environment.

The project focuses on:

- Strict layering
- Explicit domain rules
- Controlled dependencies
- Consistent error handling
- Testable application logic

The goal is not feature breadth, but structural clarity and enforceable boundaries.

---

## Documentation

<p>
  <a href="https://goldbarth.github.io/ServiceDeskLite/index.html">
    <img src="https://img.shields.io/badge/Docs-DocFX-2C3E50?logo=readthedocs&logoColor=white" alt="DocFX Docs" />
  </a>
  <a href="https://goldbarth.github.io/ServiceDeskLite/api/openapi.html">
    <img src="https://img.shields.io/badge/API-OpenAPI%20(Swagger)-2C3E50?logo=swagger&logoColor=white" alt="OpenAPI Swagger" />
  </a>
  <a href="./docs">
    <img src="https://img.shields.io/badge/Docs-Source-2C3E50?logo=github&logoColor=white" alt="Docs Source" />
  </a>
</p>

- **Architecture Overview**  
  https://goldbarth.github.io/ServiceDeskLite/architecture/overview.html

- **Architectural Decision Records (ADR)**  
  https://goldbarth.github.io/ServiceDeskLite/adr/index.html

- **API Reference (OpenAPI / ReDoc)**  
  https://goldbarth.github.io/ServiceDeskLite/api/openapi.html

Documentation is versioned and reviewed via pull requests.  
The `/docs` folder is the source of truth.

---

## Run with Docker

> One-command demo start — no local .NET SDK or database required.

**Prerequisites:** [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose plugin)

1. Start Docker Desktop and wait until it shows **"Engine running"**
2. Open a terminal (PowerShell, CMD, or bash) and navigate to the repository root:
   ```bash
   cd path/to/ServiceDeskLite
   ```
3. Run:
   ```bash
   docker compose up --build
   ```

This starts:
- **PostgreSQL 17** — database with a persistent named volume
- **API** — waits for the database to be healthy, then runs migrations automatically

| Endpoint | URL |
|---|---|
| API (Tickets) | `http://localhost:8080/api/v1/tickets` |

> Swagger UI and OpenAPI JSON are only available in `Development` mode, not in the Docker container.

**Stop and keep data:**
```bash
docker compose down
```

**Stop and reset data:**
```bash
docker compose down -v
```

The `postgres_data` volume persists between restarts. Use `-v` for a clean slate.

---

## Architectural Characteristics

- Domain layer without external dependencies
- Application layer depends only on Domain
- Infrastructure implements interfaces defined inward
- API layer coordinates use cases, not business rules
- No outward dependency references allowed

### Error Handling Strategy

- Result-based application flow
- No exceptions crossing application boundaries
- RFC 9457 ProblemDetails as HTTP contract
- Explicit failure modeling in use cases

### Persistence Strategy

- EF Core with PostgreSQL (production and Docker)
- InMemory provider for development and test scenarios
- Provider switch via configuration (`Persistence:Provider`)
- Explicit UnitOfWork commit boundary

### Testing Strategy

- End-to-end tests for both persistence providers
- Application layer testable without web host
- Infrastructure isolated behind interfaces

---

## Layer Structure


```
┌─────────────────────────────────────┐
│              Web (Blazor)           │  ─► Contracts
├─────────────────────────────────────┤
│           API (Minimal API)         │  ─► Application, Contracts, Domain,
│                                     │      Infrastructure, Infrastructure.InMemory
├───────────────────┬─────────────────┤
│  Infrastructure   │  Infra.InMemory │  ─► Application, Domain
├───────────────────┴─────────────────┤
│           Application               │  ─► Domain
├─────────────────────────────────────┤
│              Domain                 │  (no external dependencies)
└─────────────────────────────────────┘
```


Dependency direction is strictly inward.  
Violations are treated as blocking issues.

---

## Review Entry Points

For architectural evaluation:

1. `architecture/overview`
2. `adr/`
3. `Application` use case implementations
4. Dependency graph between projects
5. Error handling flow from Domain to API

---

## Design Intent

The project demonstrates how strict layering and explicit error modeling can:

- Reduce accidental coupling
- Keep business rules independent of infrastructure
- Limit refactoring risk
- Make behavior testable without runtime environment
