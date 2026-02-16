# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Take on the role of an experienced Senior .NET Architect!

## Build & Test Commands

.NET 10 SDK (pinned in `global.json`). Solution uses `ServiceDeskLite.slnx` (XML format).

```bash
dotnet restore ./ServiceDeskLite.slnx
dotnet build ./ServiceDeskLite.slnx
dotnet test ./ServiceDeskLite.slnx

# Run a single test project
dotnet test tests/ServiceDeskLite.Tests.Domain/ServiceDeskLite.Tests.Domain.csproj

# Run a single test by name filter
dotnet test ./ServiceDeskLite.slnx --filter "FullyQualifiedName~CreateTicketHandlerTests"

# Run the web app
dotnet run --project src/ServiceDeskLite.Web/ServiceDeskLite.Web.csproj
```

Lock files are enforced globally (`RestorePackagesWithLockFile=true` in `Directory.Build.props`). After adding/updating packages, regenerate with `dotnet restore --force-evaluate`.

## Architecture

Clean/Ports-and-Adapters architecture with dependencies pointing inward:

```
Domain  <--  Application  <--  Infrastructure / Infrastructure.InMemory  <--  Web
```

| Project | Role |
|---------|------|
| **Domain** | Entities, value objects, enums, workflow rules, `Guard` utilities. Zero external dependencies. |
| **Application** | Use-case handlers (commands/queries), port interfaces (`ITicketRepository`, `IUnitOfWork`), `Result<T>` type, `ApplicationError`, `DomainExceptionMapper`. No MediatR. |
| **Infrastructure** | EF Core + SQLite implementation of ports. |
| **Infrastructure.InMemory** | Pure in-memory implementation using `ConcurrentDictionary`. |
| **Web** | Blazor Interactive Server UI. Selects persistence provider via `Persistence:Provider` config (`InMemory` or `Sqlite`). |

### Domain Model

Single aggregate: `Ticket` (mutable class, not a record). `TicketId` is a `readonly record struct` wrapping `Guid`. Status transitions enforced by `TicketWorkflow` static class. Invariants enforced by `Guard` (throws `DomainException`).

### Use Case Pattern

Each use case has its own folder under `Tickets/<UseCaseName>/` containing a command/query record, a handler class, and a result record. Handlers are plain classes with `HandleAsync(TRequest?, CancellationToken)` — no interface, no MediatR pipeline. Validation is done manually at the top of each handler.

### Result & Error Pattern

Custom `Result`/`Result<T>` in `Application.Common`. `ApplicationError` is a `sealed record(Code, Message, ErrorType, Meta?)` with types: `Validation`, `NotFound`, `Conflict`, `DomainViolation`, `Unexpected`. Static factories: `Result<T>.Validation(code, msg)`, `.NotFound(code, msg)`, `.DomainViolation(code, msg)`.

Domain exceptions are bridged via `DomainExceptionMapper.ToApplicationError(DomainException)`.

### Error Code Conventions

- Domain: `domain.<aggregate>.<field>.<rule>` (e.g., `domain.ticket.status.invalid_transition`)
- Application: `<use_case>.<field>.<rule>` (e.g., `create_ticket.title.required`)

## Commit Convention

Pattern: `type(scope): short description` (imperative mood)

Types: `feat`, `fix`, `refactor`, `chore`, `docs`, `test`, `build`, `ci`, `perf`, `revert`

Scopes: `web`, `domain`, `repo`, `app`, `infra`, `ci`

If a change affects multiple layers, split the commit or omit the scope.

## Test Conventions

- xUnit + FluentAssertions (v8) + coverlet
- Test method naming: descriptive snake_case (e.g., `Returns_validation_error_when_title_missing`)
- Application tests use **inline private Fake classes** implementing port interfaces (no Moq/NSubstitute)
- Domain tests use `[Fact]` with FluentAssertions `.Should()` chains

## Global Build Properties

`Directory.Build.props` enables: `Nullable`, `ImplicitUsings`, `LangVersion latest`. Test projects add a global `using Xunit;`.

## Code Style

Key `.editorconfig` rules: UTF-8, LF line endings, 4-space indent, `var` preferred when type is apparent, no `this.` qualification, system usings sorted first and separated.
