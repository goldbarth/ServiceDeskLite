## Project Overview

ServiceDeskLite is a **.NET 10 showcase application** demonstrating clean layered architecture, explicit domain workflow handling, and a proper error strategy (RFC 9457 ProblemDetails). It consists of two runnable applications:

- **API** – ASP.NET Core Minimal API (`src/ServiceDeskLite.Api`)
- **Web** – Blazor Interactive Server frontend (`src/ServiceDeskLite.Web`)

---

## Tech Stack

| Concern    | Technology                                                            |
|------------|-----------------------------------------------------------------------|
| Runtime    | .NET 10 (SDK 10.0.102, `global.json` pinned)                          |
| API        | ASP.NET Core Minimal API                                              |
| UI         | Blazor Interactive Server, MudBlazor, Bootstrap 5                     |
| ORM        | EF Core 10, SQLite (production), In-Memory (dev/test)                 |
| Logging    | Serilog (Console sink, machine name + thread enrichers)               |
| Testing    | xUnit, FluentAssertions, Microsoft.AspNetCore.Mvc.Testing             |
| API docs   | Microsoft.AspNetCore.OpenApi (built-in)                               |
| Lock files | `packages.lock.json` per project (`RestorePackagesWithLockFile=true`) |

---

## Solution Structure

```
ServiceDeskLite/
├── src/
│ ├── ServiceDeskLite.Domain/ # Aggregate, enums, Guard, exceptions
│ ├── ServiceDeskLite.Application/ # Use-case handlers, Result<T>, abstractions
│ ├── ServiceDeskLite.Contracts/ # Versioned HTTP DTOs (V1)
│ ├── ServiceDeskLite.Infrastructure/ # EF Core + SQLite persistence
│ ├── ServiceDeskLite.Infrastructure.InMemory/ # In-memory persistence
│ ├── ServiceDeskLite.Api/ # Minimal API host + composition root
│ └── ServiceDeskLite.Web/ # Blazor UI host + API client
├── tests/
│ ├── ServiceDeskLite.Tests.Domain/
│ ├── ServiceDeskLite.Tests.Application/
│ ├── ServiceDeskLite.Tests.Api/
│ ├── ServiceDeskLite.Tests.Infrastructure.InMemory/
│ ├── ServiceDeskLite.Tests.EndToEnd/
│ ├── ServiceDeskLite.Tests.Integration/
│ └── ServiceDeskLite.Tests.Web/
├── docs/
│ └── PROJECTSTRUCTURE.md
├── requests/
│ └── api.http # HTTP request samples (IDE tooling)
├── Directory.Build.props # Solution-wide MSBuild defaults
├── global.json # SDK version pin
└── ServiceDeskLite.slnx # Solution file
```

## Dependency Rules

Dependencies must point strictly **inward**:

```
Web → Contracts
Api → Application, Contracts, Domain, Infrastructure, Infrastructure.InMemory
Application → Domain
Infrastructure → Application, Domain
Infrastructure.InMemory → Application, Domain
Domain → (nothing – zero external dependencies)
```

**No layer may import from a layer that is outer to it.** Violating this is a blocking issue.

---

## Domain Layer (`ServiceDeskLite.Domain`)

### `Ticket` Aggregate Root

```csharp
public sealed class Ticket
{
    public TicketId Id { get; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? DueAt { get; private set; }

    public Ticket(
        TicketId id,
        string title,
        string description,
        TicketPriority priority,
        DateTimeOffset createdAt,
        DateTimeOffset? dueAt = null)   // Guard validates all inputs

    public void ChangeStatus(TicketStatus newStatus)   // Delegates to TicketWorkflow
}
```
`Ticket` is constructed via its constructor – no factory method, no ORM-only parameterless constructor visible externally. Guard validation runs inside the constructor.

### `TicketId` – Strongly-Typed ID

```csharp
public readonly record struct TicketId(Guid Value)
{
    public static TicketId New() => new(Guid.NewGuid());
}
```

### Enums

```csharp
public enum TicketStatus  { New, Triaged, InProgress, Waiting, Resolved, Closed }
public enum TicketPriority { Low, Medium, High, Critical }
```

### `TicketWorkflow` – Status Transition Rules

```csharp
private static readonly HashSet<(TicketStatus From, TicketStatus To)> _allowed =
[
    (New,        Triaged),
    (Triaged,    InProgress),
    (Triaged,    Waiting),
    (Triaged,    Resolved),
    (InProgress, Waiting),
    (InProgress, Resolved),
    (Waiting,    InProgress),
    (Waiting,    Resolved),
    (Resolved,   Closed),
    (Resolved,   InProgress),   // Reopen path
];

public static bool CanTransition(TicketStatus from, TicketStatus to)
public static void EnsureCanTransition(TicketStatus from, TicketStatus to)
// Throws DomainException with code "domain.ticket.status.invalid_transition"
```

### Visual workflow:

```
New → Triaged → InProgress ⇄ Waiting
                    ↓              ↓
                Resolved ←────────┘
                    ↓
                 Closed
                    ↑ (Reopen: Resolved → InProgress)

```

### `Guard` – Invariant Enforcement

```csharp
public static class Guard
{
    // Throws DomainException(code: "domain.not_empty")
    public static void NotNullOrWhiteSpace(string? value, string paramName)

    // Throws DomainException(code: "domain.max_length")
    public static void MaxLength(string value, int maxLength, string paramName)

    // Throws DomainException(code: "guard.not_null")
    public static void NotNull<T>(T? value, string paramName) where T : class
}
```

### Exception Types

```csharp
public sealed record DomainError(string Code, string Message);

public sealed class DomainException(DomainError error) : Exception(error.Message)
{
    public DomainError Error { get; } = error;
}
```

Domain exceptions are caught in Application handlers and mapped to `Result.DomainViolation(...)`. They must never reach the HTTP layer as raw exceptions.

---

## Application Layer (`ServiceDeskLite.Application`)

#### Result Pattern

Handlers never throw. All outcomes are expressed via `Result` or `Result<T>`.

#### `Result` (void success)

```csharp
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public ApplicationError? Error { get; }

    public static Result Success()
    public static Result Failure(ApplicationError error)
    public static Result NotFound(string code, string message,
        IReadOnlyDictionary<string, object>? meta = null)
    public static Result Validation(string code, string message,
        IReadOnlyDictionary<string, object>? meta = null)
    public static Result DomainViolation(string code, string message,
        IReadOnlyDictionary<string, object>? meta = null)
}
```

#### `Result<T>` (value on success)

```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }       // Throws InvalidOperationException if IsFailure
    public ApplicationError? Error { get; }

    public static Result<T> Success(T value)
    public static Result<T> Failure(ApplicationError error)
    public static Result<T> NotFound(string code, string message,
        IReadOnlyDictionary<string, object>? meta = null)
    public static Result<T> Validation(string code, string message,
        IReadOnlyDictionary<string, object>? meta = null)
    public static Result<T> DomainViolation(string code, string message,
        IReadOnlyDictionary<string, object>? meta = null)
}
```

#### `ApplicationError`

```csharp
public sealed record ApplicationError(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, object>? Meta = null)
{
    public static ApplicationError Validation(string code, string message, ...)
    public static ApplicationError NotFound(string code, string message, ...)
    public static ApplicationError Conflict(string code, string message, ...)
    public static ApplicationError DomainViolation(string code, string message, ...)
    public static ApplicationError Unexpected(string code, string message, ...)
}
```

#### `ErrorType` → HTTP Status Mapping

| ErrorType         | 	HTTP  Status |
|-------------------|---------------|
| `Validation`      | 400           |
| `DomainViolation` | 	400          |
| `NotFound`        | 404           |
| `Conflict`	       | 409           |
| `Unexpected`      | 500           |

#### Use Case Handlers

Each use case lives in its own folder under `Application/Tickets/<UseCase>/`. Structure:

- `<UseCase>Command.cs` or `<UseCase>Query.cs` – input record
- `<UseCase>Handler.cs` – handler with `HandleAsync` method
- `<UseCase>Result.cs` or `<UseCase>Dto.cs` – output record/DTO

#### Handler signature contract:

```csharp
public async Task<Result<TOutput>> HandleAsync(TInput? input, CancellationToken ct = default)
// Null input → return Result<T>.Validation(...), never throw
```

#### `CreateTicket`

```csharp
public sealed record CreateTicketCommand(
    string Title,
    string Description,
    TicketPriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt = null);

public sealed record CreateTicketResult(TicketId Id);

// Handler: validates null, catches DomainException, checks duplicate (ExistsAsync),
//          AddAsync + SaveChangesAsync
public sealed class CreateTicketHandler
{
    public async Task<Result<CreateTicketResult>> HandleAsync(
        CreateTicketCommand? command, CancellationToken ct = default)
}
```

#### `GetTicketById`

```csharp
public sealed record GetTicketByIdQuery(TicketId Id);

public record TicketDetailsDto(
    TicketId Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt);

public sealed class GetTicketByIdHandler
{
    public async Task<Result<TicketDetailsDto>> HandleAsync(
        GetTicketByIdQuery? query, CancellationToken ct = default)
}
```

#### `SearchTickets`

```csharp
public sealed record SearchTicketsQuery(
    TicketSearchCriteria Criteria,
    Paging Paging,
    SortSpec? Sort = null);

public sealed record SearchTickesResult(PagedResult<TicketListItemDto> Page);

public class SearchTicketsHandler
{
    public async Task<Result<SearchTickesResult>> HandleAsync(
        SearchTicketsQuery? query, CancellationToken ct = default)
}
```

#### Shared Application Types

```csharp
public sealed record TicketSearchCriteria(
    string? Text = null,
    IReadOnlyCollection<TicketStatus>? Statuses = null,
    IReadOnlyCollection<TicketPriority>? Priorities = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    DateTimeOffset? DueFrom = null,
    DateTimeOffset? DueTo = null);

public readonly record struct Paging(int Page, int PageSize)
{
    public int Skip => (Page - 1) * PageSize;
}

public enum SortDirection { Asc, Desc }

public enum TicketSortField { CreatedAt, DueAt, Priority, Status, Title }

public readonly record struct SortSpec(TicketSortField Field, SortDirection Direction)
{
    public static SortSpec Default => new(TicketSortField.CreatedAt, SortDirection.Desc);
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    Paging Paging);

public record TicketListItemDto(
    TicketId Id,
    string Title,
    TicketStatus Status,
    TicketPriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt);
```

#### Paging Policy Constants

```csharp
public static class PagingPolicy
{
    public const int MinPage = 1;
    public const int MinPageSize = 1;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 200;
}
```

#### Repository & Unit of Work Abstractions

```csharp
public interface ITicketRepository
{
    Task AddAsync(Ticket ticket, CancellationToken ct = default);
    Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default);
    Task<bool> ExistsAsync(TicketId id, CancellationToken ct = default);
    Task<PagedResult<Ticket>> SearchAsync(
        TicketSearchCriteria criteria,
        Paging paging,
        SortSpec sort,
        CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

---

## Contracts Layer (`ServiceDeskLite.Contracts`)

All public HTTP types are versioned under `V1/`. Enum properties in responses are strings (not numeric), enforced by `JsonStringEnumConverter` in the API.

#### Request / Response DTOs

```csharp
// POST /api/v1/tickets
public sealed record CreateTicketRequest(
    string Title,
    string Description,
    TicketPriority Priority,
    DateTimeOffset? DueAt);

public sealed record CreateTicketResponse(Guid Id);

// GET /api/v1/tickets/{id}
public sealed record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    string Priority,        // string – serialized enum value
    string Status,          // string – serialized enum value
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt);

// GET /api/v1/tickets
public sealed record SearchTicketsRequest(
    int Page = 1,
    int PageSize = 25,
    TicketSortField? SortField = null,
    SortDirection? SortDirection = null);

public sealed record TicketListItemResponse(
    Guid Id,
    string Title,
    string Priority,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}
```

#### Contracts Enums

```csharp
public enum TicketPriority  { Low, Medium, High, Critical }
public enum TicketSortField { CreatedAt, DueAt, Priority, Status, Title }
public enum SortDirection   { Asc, Desc }
```

#### ProblemDetails Extension Keys

```csharp
public static class ContractsProblemDetailsConventions
{
    public static class ExtensionKeys
    {
        public const string Code      = "code";
        public const string ErrorType = "errorType";
        public const string Meta      = "meta";
        public const string TraceId   = "traceId";
    }
}
```

---

## API Layer (`ServiceDeskLite.Api`)

#### Middleware Pipeline Order (`Program.cs`)

```
1.  Serilog configuration (before WebApplication builder)
2.  Services:
      AddApiDocumentation        → OpenAPI
      AddApiErrorHandling        → ProblemDetails + ExceptionHandler + Mapper
      AddApplication             → use-case handlers (Scoped)
      AddApiInfrastructure       → persistence provider switch
3.  EF Core auto-migration (Sqlite only)
4.  app.UseSerilogRequestLogging()
5.  app.UseApiDocumentation()
6.  app.UseApiErrorHandling()     → UseExceptionHandler()
7.  app.UseHttpsRedirection()
8.  app.UseCors("WebDev")         → https://localhost:7023 only
9.  Endpoint mapping
```

#### Endpoints (`TicketsEndpoints.cs`)

Base route group: `/api/v1/tickets`

| HTTP	 | Route                     | Handle              | Returns                                         |
|-------|---------------------------|---------------------|-------------------------------------------------|
| POST	 | /api/v1/tickets           | 	CreateTicketAsync	 | 201 Created + CreateTicketResponse              |
| GET	  | /api/v1/tickets/{id:guid} | 	GetTicketByIdAsync | 	200 OK + TicketResponse                        |
| GET	  | /api/v1/tickets           | 	SearchTicketsAsync | 	200 OK + PagedResponse<TicketListItemResponse> |

All errors return RFC 9457 ProblemDetails via `ResultToProblemDetailsMapper`.

#### Correlation

```csharp
public static class Correlation
{
    public static string GetTraceId(HttpContext ctx)
        => Activity.Current?.Id
           ?? ctx.TraceIdentifier
           ?? "unknown";
}
```

TraceId is attached to every ProblemDetails response as the `traceId` extension field.

#### `ResultToProblemDetailsMapper`

```csharp
public sealed class ResultToProblemDetailsMapper
{
    public IResult ToHttpResult<T>(
        HttpContext ctx, Result<T> result, Func<T, IResult> onSuccess)

    public IResult ToProblem(HttpContext ctx, ApplicationError error)
}
```
Uses `ApiProblemDetailsFactory` to produce RFC 9457 responses with extensions:
`code`, `errorType`, `traceId`, `meta`.

#### `ResultMappingExtensions`

Fluent bridge: `result.ToHttpResult(ctx, mapper, value => Results.Ok(value.ToResponse()))`.

#### Exception Handling Pipeline

- `ApiExceptionHandler : IExceptionHandler` catches all unhandled exceptions
- `ExceptionClassification.IsClientBadRequest(ex)` → `BadHttpRequestException, FormatException, InvalidOperationException` → 400
- `ExceptionClassification.IsCancellation(ex, ctx)` → request cancelled → no response
- All other exceptions → 500 Unexpected
  Logging: 5xx → ERROR, 409 → WARNING, 400 → WARNING, others → INFO.

#### Enum Mapping (`Api/Mapping/Tickets/TicketEnumMapping.cs`)

```csharp
// Contracts → Domain
public static DomainTicketPriority ToDomain(this TicketPriority value)

// Contracts → Application
public static AppTicketSortField  ToApplication(this TicketSortField value)
public static AppSortDirection    ToApplication(this SortDirection value)

// Application → Contracts
public static TicketResponse           ToResponse(this TicketDetailsDto dto)
public static TicketListItemResponse   ToListItemResponse(this TicketListItemDto dto)
public static Paging                   ToPaging(this SearchTicketsRequest request)
public static SortSpec?                ToSort(this SearchTicketsRequest request)
public static PagedResponse<TicketListItemResponse>
    ToPagedResponse(this PagedResult<TicketListItemDto> page)
```

#### `appsettings.json` (Production)

```json
{
    "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
    "Persistence": { "Provider": "Sqlite" },
    "ConnectionStrings": { "ServiceDeskLite": "Data Source=servicedesklite.db" },
    "AllowedHosts": "*"
}
```

#### `appsettings.Development.json`

```json
{
    "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
    "Persistence": { "Provider": "InMemory" }
}
```

## Infrastructure – SQLite (`ServiceDeskLite.Infrastructure`)

#### `ServiceDeskLiteDbContext`

```csharp
public class ServiceDeskLiteDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    // Fluent config via IEntityTypeConfiguration<T> assemblies
}
```

#### EF Core Fluent Configuration (`TicketConfiguration`)

```csharp
builder.ToTable("Tickets");
builder.HasKey(t => t.Id);
builder.Property(t => t.Id)
    .HasConversion(new TicketIdConverter()).ValueGeneratedNever();
builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
builder.Property(t => t.Description).IsRequired().HasMaxLength(2000);
builder.Property(t => t.Priority).IsRequired();
builder.Property(t => t.Status).IsRequired();
builder.Property(t => t.CreatedAt)
    .HasConversion(new DateTimeOffsetToBinaryConverter()).IsRequired();
builder.Property(t => t.DueAt).IsRequired(false);
builder.HasIndex(t => t.CreatedAt);
builder.HasIndex(t => t.Status);
```

Value converter: `TicketId` ↔ `Guid` via `TicketIdConverter`.
#### Migrations

Currently, one migration: `20260219091433_InitialCreate` (Initial schema).
Migrations are auto-applied on startup when `Provider = Sqlite`.

#### EfTicketRepository
- `AddAsync` → `_dbContext.Tickets.AddAsync`
- `GetByIdAsync` → `FirstOrDefaultAsync(t => t.Id == id)`
- `ExistsAsync` → `AnyAsync(t => t.Id == id)`
- `SearchAsync` → LINQ with filters, sort, paging; tie-breaker: `.ThenBy(t => t.Id)`
-
#### `EfUnitOfWork`

```csharp
public Task SaveChangesAsync(CancellationToken ct = default)
    => _dbContext.SaveChangesAsync(ct);
```
--- 
## Infrastructure – InMemory (`ServiceDeskLite.Infrastructure.InMemory`)

#### `InMemoryStore` – Singleton

```csharp
internal sealed class InMemoryStore
{
    private readonly ConcurrentDictionary<TicketId, Ticket> _tickets = new();

    public bool TryGetTicket(TicketId id, out Ticket? ticket)
    public bool ContainsTicket(TicketId id)
    public IReadOnlyCollection<Ticket> SnapshotTickets()
    public void ApplyAdds(IEnumerable<Ticket> adds)   // Throws on duplicate
}
```
Data persists for the application lifetime. Shared across all scoped requests.

#### `InMemoryUnitOfWork` – Scoped

```csharp
internal sealed class InMemoryUnitOfWork : IUnitOfWork
{
    internal List<object> PendingAdds { get; } = [];

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        var ticketAdds = PendingAdds.OfType<Ticket>().ToArray();
        if (ticketAdds.Length > 0) _store.ApplyAdds(ticketAdds);
        PendingAdds.Clear();
    }
}
```

#### InMemoryTicketRepository – Scoped

- `AddAsync` → appends to `unitOfWork.PendingAdds`
- `GetByIdAsync` / `ExistsAsync` → synchronous lookup on `InMemoryStore`
- `SearchAsync` → LINQ in-memory with same filter/sort logic; tie-breaker: `.ThenBy(t => t.Id.Value)`

#### DI Lifetime Summary

| Type                       | 	Lifetime | 	Reason                        |
|----------------------------|-----------|--------------------------------|
| `InMemoryStore`            | Singleton | 	Shared in-process state       |
| `InMemoryUnitOfWork`       | 	Scoped   | 	Per-request change set        |
| `InMemoryTicketRepository` | 	Scoped   | 	References scoped UoW         |
| `EfTicketRepository`       | 	Scoped   | 	DbContext is Scoped           |
| `EfUnitOfWork`             | 	Scoped   | 	DbContext is Scoped           |
| `Use-case handlers         | 	Scoped   | 	Reference Scoped repositories |

---

## Web Layer (`ServiceDeskLite.Web`)

#### API Client

```csharp
public interface ITicketsApiClient
{
    Task<ApiResult<PagedResponse<TicketListItemResponse>>> SearchAsync(
        SearchTicketsRequest request, CancellationToken ct = default);

    Task<ApiResult<TicketResponse>> GetByIdAsync(
        Guid id, CancellationToken ct = default);

    Task<ApiResult<CreateTicketResponse>> CreateAsync(
        CreateTicketRequest request, CancellationToken ct = default);
}
```
`TicketsApiClient` uses `HttpClient` with `PropertyNameCaseInsensitive` JSON deserialization and parses ProblemDetails from API error responses.

#### `ApiResult<T>` and `ApiError`

```csharp
public sealed class ApiResult<T>
{
    public bool IsSuccess => Error is null;
    public T? Value { get; }
    public ApiError? Error { get; }
}

public sealed class ApiError
{
    public int Status { get; init; }
    public string? Title { get; init; }
    public string? Detail { get; init; }
    public string? Code { get; init; }
    public string? ErrorType { get; init; }
    public string? TraceId { get; init; }
    public Dictionary<string, object>? Meta { get; init; }
}
```
#### Configuration

```json lines
// appsettings.Development.json (Web)
{
    "ApiClient": {
        "BaseUrl": "https://localhost:7238",
        "TimeoutSeconds": 10
    }
}
```
---

#### Common Commands

#### Build and Test

```
# Restore (uses lock files – required before first build)
dotnet restore ./ServiceDeskLite.slnx

# Build
dotnet build ./ServiceDeskLite.slnx -c Release

# Run all tests
dotnet test ./ServiceDeskLite.slnx -c Release

# Run a single test project
dotnet test tests/ServiceDeskLite.Tests.Domain/

# Run specific test by name filter
dotnet test --filter "FullyQualifiedName~TicketTests"
```

#### Run Applications

```csharp
# API (defaults to InMemory in Development)
dotnet run --project src/ServiceDeskLite.Api

# Web (Blazor frontend)
dotnet run --project src/ServiceDeskLite.Web
```

#### EF Core Migrations

```
# Add a new migration (from repo root)
dotnet ef migrations add <MigrationName> \
  --project src/ServiceDeskLite.Infrastructure \
  --startup-project src/ServiceDeskLite.Api

# Apply pending migrations
dotnet ef database update \
  --project src/ServiceDeskLite.Infrastructure \
  --startup-project src/ServiceDeskLite.Api
```
---

#### Testing Conventions

#### Project Layout Mirrors Source

```
tests/ServiceDeskLite.Tests.Application/Tickets/CreateTicket/
  ↳ tests src/ServiceDeskLite.Application/Tickets/CreateTicket/
```

#### Test Naming

`<MethodOrScenario>_<Condition>_<ExpectedOutcome>`

Examples:

- `NewTicket_StartsWithStatus_New`
- `ChangeStatus_InvalidTransition_ThrowsDomainException_WithExpectedErrorCode`
- `HandleAsync_NullCommand_ReturnsValidationFailure`
-
#### Assertion Library

All tests use FluentAssertions. Prefer `.Should().Be(...)` over `Assert.Equal.`

#### Test Projects

| Project                         | 	Scope                                  | 	Key Patterns                                                          |
|---------------------------------|-----------------------------------------|------------------------------------------------------------------------|
| `Tests.Domain`                  | 	Domain aggregate, workflow transitions | 	`Ticket` constructor, `TicketWorkflow` guard v                        |
| `Tests.Application`             | 	Handler logic, Result monad            | 	`CreateTicketHandler`, `GetTicketByIdHandler`, `SearchTicketsHandler` |
| `Tests.Api`                     | 	HTTP binding, exception handling       | 	`ApiWebApplicationFactory` (InMemory)                                 |
| `Tests.Infrastructure.InMemory` | 	In-memory repository                   | 	`InMemoryTicketRepository`                                            |
| `Tests.EndToEnd`                | 	Full DI pipeline, both providers       | 	`[ProviderMatrix]` attribute                                          |
| `Tests.Integration`             | 	HTTP query binding                     | 	`SearchTicketsRequest` parameter parsing                              |
| `Tests.Web`                     | 	API client deserialization             | 	`TicketsApiClient` + ProblemDetails parsing                           |

#### End-to-End Tests (`[ProviderMatrix]`)

```csharp
[Theory, ProviderMatrix]
public async Task CreateThenFind_ReturnsTicket(PersistenceProvider provider)
{
    await using var services = TestServiceProvider.Create(provider);
    // ...
}
```

TestServiceProvider.Create(provider) boots a full DI container with either
InMemory or Sqlite (in-memory SQLite DB). Key E2E scenarios:

- `CommitBoundaryTests` – Unit of Work flush behavior
- `DuplicateDetectionTests` – Conflict result path
- `DeterministicPagingSortingTests` – `Stable CreatedAt + Id` ordering
- `ReadIsolationTests` – No uncommitted-read leakage between scopes API Tests

`ApiWebApplicationFactory` inherits `WebApplicationFactory<Program>` and overrides
persistence with InMemory.

---

#### Code Conventions

#### C# Style (`.editorconfig`)

- Indent: 4 spaces
- `var` for built-in types and when type is apparent
- `System` usings first, separated from other groups
- Nullable reference types: enabled everywhere
- Implicit usings: enabled
- `LangVersion: latest`
- Line endings: `lf`, `insert_final_newline = true`

##### Naming

| Pattern               | 	Example                                                   |
|-----------------------|------------------------------------------------------------|
| Error codes           | 	`dot.separated.snake_case – create_ticket.title.required` |
| Handler method	       | `HandleAsync(TInput? input, CancellationToken ct)`         |
| Repository methods    | 	`AddAsync, GetByIdAsync, ExistsAsync, SearchAsync`        |
| DI extension methods	 | `Add<Feature>() on IServiceCollection`                     |

#### Null Safety

- Handlers validate null inputs: return `Result.Validation(...)`, never throw
- Domain `Guard` methods throw `DomainException` (caught in handlers, mapped to Result)
- `OperationCanceledException` is never caught or swallowed

#### MediatR

MediatR is **not used in Milestone 1**. Handlers are registered directly in DI and injected into endpoints as typed parameters. MediatR may be introduced in a later milestone – do not add it prematurely.

#### JSON Enum Serialization

Enum values in API responses are strings (not integers). Enforced by `JsonStringEnumConverter`
registered at startup. Always use string comparisons when processing API responses in the Web layer.

---

#### API Surface (Current)

| Method | 	Route                  | 	Description                 |
|--------|-------------------------|------------------------------|
| `POST` | 	`/api/v1/tickets`      | 	Create a ticket             |
| `GET`  | 	`/api/v1/tickets/{id}` | 	Get ticket by GUID          |
| `GET`  | 	`/api/v1/tickets`      | 	Search/list tickets (paged) |

Paging: page >= 1, pageSize 1–200, default 25.
Sort: deterministic – primary sort field + CreatedAt + Id tie-breaker.

---

#### Commit Conventions

Pattern: `type(optional-scope): short description` (imperative mood)

| Type       | 	Meaning                               |
|------------|----------------------------------------|
| `feat`     | 	New functionality                     |
| `fix`      | 	Bug fix                               |
| `refactor` | 	Structural change, no behavior change |
| `chore`    | 	Setup, config, dependencies           |
| `docs`     | 	Documentation only                    |
| `test`     | 	Tests only                            |
| `build`    | 	Build system changes                  |
| `ci`       | 	CI/CD configuration                   |
| `perf`     | 	Performance improvements              |
| `revert`   | 	Revert a previous commit              |

Active scopes: `web`, `domain`, `app`, `infra`, `api`, `ci`, `repo`

If a change spans multiple scopes, omit the scope or split into separate commits.

---

#### CI/CD (GitHub Actions)

`.github/workflows/ci.yml` runs on push/PR to `main`:

1. Checkout
2. Setup .NET 10
3. `dotnet restore` (with `packages.lock.json` cache)
4. `dotnet build -c Release --no-restore`
5. `dotnet test -c Release --no-build`
   Matrix: ubuntu-latest and windows-latest.

Lock files (`packages.lock.json`) are committed per project and must be kept up to date.
Run `dotnet restore` after adding/updating any NuGet package.

---

#### Key Files for Orientation

| File                                                                          | 	Purpose                                      |
|-------------------------------------------------------------------------------|-----------------------------------------------|
| `src/ServiceDeskLite.Api/Program.cs`                                          | 	API composition root and middleware pipeline |
| `src/ServiceDeskLite.Web/Program.c`                                           | 	Blazor composition root                      |
| `src/ServiceDeskLite.Api/Composition/InfrastructureComposition.cs`            | 	Provider switch (InMemory vs Sqlite)         |
| `src/ServiceDeskLite.Api/Endpoints/TicketsEndpoints.cs`	All API endpoint      | definitions                                   |
| `src/ServiceDeskLite.Api/Mapping/Tickets/TicketEnumMapping.cs`                | 	Contracts ↔ Domain/App mapping               |
| `src/ServiceDeskLite.Application/Common/Result.cs`                            | 	Result monad (void)                          |
| `src/ServiceDeskLite.Application/Common/ApplicationError.cs`                  | 	Error record + ErrorType enum                |
| `src/ServiceDeskLite.Application/Common/PagingPolicy.cs`                      | 	Paging constants                             |
| `src/ServiceDeskLite.Domain/Tickets/Ticket.cs`                                | 	Aggregate root                               |
| `src/ServiceDeskLite.Domain/Tickets/TicketWorkflow.cs`                        | 	Status transition rules                      |
| `src/ServiceDeskLite.Domain/Common/Guard.cs`                                  | 	Invariant enforcement                        |
| `src/ServiceDeskLite.Api/Http/ProblemDetails/ResultToProblemDetailsMapper.cs` | 	Result → HTTP mapping                        |
| `src/ServiceDeskLite.Infrastructure/Persistence/ServiceDeskLiteDbContext.cs`  | 	EF Core DbContext                            |
| `src/ServiceDeskLite.Infrastructure.InMemory/Persistence/InMemoryStore.cs`    | 	In-memory data store                         |
| `src/ServiceDeskLite.Web/ApiClient/TicketsApiClient.cs`                       | 	HTTP client for API                          |
| `tests/ServiceDeskLite.Tests.EndToEnd/Composition/TestServiceProvider.cs`     | 	E2E DI bootstrap                             |
| `tests/ServiceDeskLite.Tests.Api/Infrastructure/ApiWebApplicationFactory.cs`  | 	API test factory                             |
