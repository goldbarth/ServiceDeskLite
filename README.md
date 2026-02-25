# ServiceDeskLite

<p>
  <a href="https://github.com/goldbarth/ServiceDeskLite/actions/workflows/ci.yml">
    <img src="https://github.com/goldbarth/ServiceDeskLite/actions/workflows/ci.yml/badge.svg" alt="CI" />
  </a>
  <a href="https://github.com/goldbarth/ServiceDeskLite/actions/workflows/docs.yml">
    <img src="https://github.com/goldbarth/ServiceDeskLite/actions/workflows/docs.yml/badge.svg" alt="Docs" />
  </a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/License-MIT-2C3E50" alt="MIT" />
</p>

## Purpose

ServiceDeskLite is a deliberately structured engineering case built to explore and apply Clean Architecture principles in a modern .NET environment.

The project prioritizes architectural clarity over feature growth.
Design decisions, trade-offs and system boundaries are made explicit and documented.

It reflects a commitment to treating architecture as a deliberate discipline rather than an emergent byproduct of feature growth.

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
    <img src="https://img.shields.io/badge/Docs-Source%20(in%20repo)-2C3E50?logo=github&logoColor=white" alt="Docs Source" />
  </a>
</p>

- **Docs (DocFX landing page):** https://goldbarth.github.io/ServiceDeskLite/index.html
- **API Reference (Swagger / OpenAPI):** https://goldbarth.github.io/ServiceDeskLite/api/openapi.html
- **Docs Source (repository):** https://github.com/goldbarth/ServiceDeskLite/tree/main/docs

> DocFX is the reader-friendly documentation site. The `/docs` folder is the source of truth and is reviewed via pull requests.

---

## Start Here

This project is structured as an explicit engineering case.
Documentation is intentionally organized to reflect architectural intent rather than feature growth.

If you're reviewing this repository for architectural clarity, the following entry points provide the fastest overview:

- **Architecture Overview**
  High-level system structure, layering decisions and core design principles.
  → https://goldbarth.github.io/ServiceDeskLite/architecture/overview.html

- **Architectural Decision Records (ADR)**
  Explicit documentation of major design decisions and trade-offs.
  → https://goldbarth.github.io/ServiceDeskLite/adr/index.html

- **API Reference (OpenAPI / ReDoc)**
  Contract-first API surface with versioned endpoints.
  → https://goldbarth.github.io/ServiceDeskLite/api/openapi.html

<<<<<<< docs-update
=======
---

>>>>>>> main
## Architecture

ServiceDeskLite is structured as a strict layered system with inward-only dependencies.

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

No layer may reference anything from a layer outer to it. Violations are blocking issues.
