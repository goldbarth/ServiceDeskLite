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
    public static Result<T> Conflict(string code, string message,
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

#### Result & Error Type Model

![Result & Error Type Model](../assets/diagrams/result-error-type-model.svg)

#### `ErrorType` → HTTP Status Mapping

| ErrorType         | HTTP Status |
|-------------------|-------------|
| `Validation`      | 400         |
| `DomainViolation` | 400         |
| `NotFound`        | 404         |
| `Conflict`        | 409         |
| `Unexpected`      | 500         |

#### Use Case Handlers

Each use case lives in its own folder under `Application/Tickets/<UseCase>/`. Structure:

- `<UseCase>Command.cs` or `<UseCase>Query.cs` – input record
- `<UseCase>Handler.cs` – handler with `HandleAsync` method
- `<UseCase>Result.cs` or `<UseCase>Dto.cs` – output record/DTO

#### CQRS in This Project (lightweight)

This solution uses **lightweight CQRS** as an application-structure pattern, not
as a distributed systems pattern.

| Topic | Current project scope |
|---|---|
| Command / query split | Commands and queries are modeled as separate request types with dedicated handlers. |
| Write side | State-changing use cases (`CreateTicket`, `ChangeTicketStatus`, `AssignTicket`, `AddComment`) execute through command handlers. |
| Read side | Read use cases (`GetTicketById`, `SearchTickets`, `GetAuditEvents`, `GetDashboardSummary`) execute through query handlers and return read-oriented DTOs. |
| Endpoint dispatch | ASP.NET Core Minimal API endpoints inject handlers directly from DI and call `HandleAsync(...)` explicitly. |
| Persistence model | The separation is logical inside one application and one persistence boundary, not separate write and read databases. |

Deliberately **not** included in the current scope:

- No `MediatR` / `ISender` dispatch layer between endpoints and handlers.
- No `IPipelineBehavior<,>` pipeline for validation, logging, or transactions.
- No separate read store, projection daemon, or eventual consistency workflow.
- No asynchronous command bus or out-of-process message dispatch for request handling.

This boundary is intentional. The project is positioned as a reference/showcase,
so the current design favors explicit request flow and low ceremony over maximum
pattern coverage.

Extension paths if the scope changes later:

- Introduce MediatR when cross-cutting handler concerns need one uniform request pipeline.
- Introduce pipeline behaviors when validation, logging, auth, or transaction policies must apply consistently to every handler call.
- Introduce dedicated read models or a separate read store only when query complexity, performance, or projection needs justify that extra operational cost.

#### Handler signature contract:

```csharp
public async Task<Result<TOutput>> HandleAsync(TInput? input, CancellationToken ct = default)
// Null input → return Result<T>.Validation(...), never throw
```

All handlers follow the same guard-then-act pattern. `CreateTicket` is the canonical example:

![Handler Signature Contract](../assets/diagrams/handler-signature-contract.svg)

#### `CreateTicket`

```csharp
public sealed record CreateTicketCommand(
    string Title,
    string Description,
    TicketPriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt = null);

public sealed record CreateTicketResult(TicketId Id);

// Handler: validates null (command + field-level), catches DomainException,
//          AddAsync + audit record + SaveChangesAsync (atomic).
//          Duplicate IDs are not checked via ExistsAsync; uniqueness is enforced
//          by the persistence layer and mapped to Result<T>.Conflict by
//          PersistenceExceptionMapper.
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

#### `ChangeTicketStatus`

```csharp
public sealed record ChangeTicketStatusCommand(TicketId Id, TicketStatus NewStatus, string? Actor = null);

// Handler: validates null, loads ticket (NotFound if missing),
//          calls ticket.ChangeStatus, records audit event, saves via UnitOfWork.
//          invalid_transition → Result<T>.Conflict (409)
//          other DomainException → Result<T>.DomainViolation (400)
// No ICommandValidator – all fields are strongly typed (TicketId, TicketStatus).
// Transition logic is domain behaviour, not input validation (see ADR 0019).
public sealed class ChangeTicketStatusHandler
{
    public async Task<Result<TicketDetailsDto>> HandleAsync(
        ChangeTicketStatusCommand? command, CancellationToken ct = default)
}
```

Returns the updated ticket as `TicketDetailsDto` (same as `GetTicketById`).

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

#### Exception Mappers

Two helper classes centralise the translation from caught exceptions to `ApplicationError`:

```csharp
// Domain layer exceptions → ApplicationError.DomainViolation
public static class DomainExceptionMapper
{
    public static ApplicationError ToApplicationError(DomainException ex)
}

// Persistence layer exceptions → ApplicationError.Conflict or ApplicationError.Unexpected
// Conflict is detected by inspecting the exception message for keywords
// ("already exists", "duplicate", "unique", "concurrency").
public static class PersistenceExceptionMapper
{
    public static ApplicationError ToApplicationError(Exception ex)
}
```

Handlers use these instead of duplicating the mapping logic inline.

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
