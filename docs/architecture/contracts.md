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
