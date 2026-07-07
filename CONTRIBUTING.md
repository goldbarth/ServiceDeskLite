# Contributing

## Architecture

ServiceDeskLite uses a clean layered architecture with strict inward-only dependencies.

### Solution Structure

```
src/
  ServiceDeskLite.Domain/                  # Aggregate, enums, Guard, domain exceptions
  ServiceDeskLite.Application/             # Use-case handlers, Result<T>, abstractions
  ServiceDeskLite.Contracts/               # Versioned HTTP DTOs (V1/)
  ServiceDeskLite.Infrastructure/          # EF Core + PostgreSQL/pgvector persistence
  ServiceDeskLite.Infrastructure.InMemory/ # In-memory persistence (dev/test)
  ServiceDeskLite.Api/                     # Minimal API host + composition root
  ServiceDeskLite.Web/                     # Blazor Server UI + API client

tests/
  ServiceDeskLite.Tests.Domain/
  ServiceDeskLite.Tests.Application/
  ServiceDeskLite.Tests.Api/
  ServiceDeskLite.Tests.Infrastructure.InMemory/
  ServiceDeskLite.Tests.EndToEnd/
  ServiceDeskLite.Tests.Integration/
  ServiceDeskLite.Tests.Web/
```

### Dependency Rules

Dependencies must point strictly **inward**. Outer layers depend on inner layers — never the reverse.

```
Web          → Contracts
Api          → Application, Contracts, Domain, Infrastructure, Infrastructure.InMemory
Application  → Domain
Infrastructure         → Application, Domain
Infrastructure.InMemory → Application, Domain
Domain       → (nothing)
```

Violating these rules is a blocking issue. In particular:
- `Web` must not reference `Application` or `Domain` directly — only `Contracts`.
- `Domain` must have zero external NuGet or project dependencies.

---

## Build and Test

```bash
# Restore (required before first build — uses packages.lock.json)
dotnet restore ./ServiceDeskLite.slnx

# Build
dotnet build ./ServiceDeskLite.slnx -c Release

# Run all tests
dotnet test ./ServiceDeskLite.slnx -c Release

# Run a single test project
dotnet test tests/ServiceDeskLite.Tests.Domain/
```

After adding or updating any NuGet package, regenerate lock files and commit them:

```bash
dotnet restore ./ServiceDeskLite.slnx
```

---

## Commit Convention

Pattern: `type(optional-scope): short description` (imperative mood)

```
feat(api): add ticket status change endpoint
fix(domain): correct invalid transition guard message
chore(ci): add windows runner to matrix
docs(adr): add ADR 0018 for authentication strategy
```

### Types

| Type       | Meaning                                |
|------------|----------------------------------------|
| `feat`     | New functionality                      |
| `fix`      | Bug fix                                |
| `refactor` | Structural change, no behavior change  |
| `chore`    | Setup, config, dependencies            |
| `docs`     | Documentation only                     |
| `test`     | Tests only                             |
| `build`    | Build system changes                   |
| `ci`       | CI/CD configuration                    |
| `perf`     | Performance improvements               |
| `revert`   | Revert a previous commit               |

### Scopes

| Scope    | Layer / area                            |
|----------|-----------------------------------------|
| `domain` | Domain layer                            |
| `app`    | Application layer (handlers, Result)    |
| `infra`  | Infrastructure (EF Core, PostgreSQL)    |
| `api`    | API host, endpoints, composition root   |
| `web`    | Blazor UI, API client                   |
| `ci`     | CI/CD workflows                         |
| `repo`   | Repository-level changes (meta, config) |

If a change spans multiple scopes, omit the scope or split into separate commits.

---

## Pull Requests

Before opening a PR:

- [ ] Solution builds without warnings (`dotnet build -c Release`)
- [ ] All tests pass (`dotnet test -c Release`)
- [ ] Lock files updated if NuGet packages were added or changed
- [ ] No debug code, commented-out code, or TODO left unresolved
- [ ] Change is focused — one concern per PR where possible
- [ ] ADR added or updated if an architectural decision was made

Use imperative mood in commit messages:

```
✔  add ticket search endpoint
✘  added ticket search endpoint
```
