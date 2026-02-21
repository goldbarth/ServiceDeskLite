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

#### Client / API Result Types

```mermaid
classDiagram
    class ITicketsApiClient {
        <<interface>>
        +SearchAsync(request, ct) Task
        +GetByIdAsync(id, ct) Task
        +CreateAsync(request, ct) Task
    }
    class TicketsApiClient {
        -HttpClient _http
        +SearchAsync(request, ct) Task
        +GetByIdAsync(id, ct) Task
        +CreateAsync(request, ct) Task
    }
    class ApiResultT["ApiResult~T~"] {
        +bool IsSuccess
        +T? Value
        +ApiError? Error
    }
    class ApiError {
        +int Status
        +string? Title
        +string? Detail
        +string? Code
        +string? ErrorType
        +string? TraceId
        +Dictionary~string,object~? Meta
    }

    TicketsApiClient ..|> ITicketsApiClient : implements
    ApiResultT --> ApiError : contains on failure
```

#### API Client Call Flow

```mermaid
sequenceDiagram
    participant Cmp as Blazor Component
    participant AC as TicketsApiClient
    participant HC as HttpClient
    participant Api as ServiceDeskLite.API

    Cmp->>+AC: SearchAsync(request)
    AC->>+HC: GetAsync(/api/v1/tickets?...)
    HC->>+Api: GET /api/v1/tickets
    Api-->>-HC: 200 OK + JSON body
    HC-->>-AC: HttpResponseMessage

    alt IsSuccessStatusCode
        AC->>AC: Deserialize PagedResponse~TicketListItemResponse~
        AC-->>Cmp: ApiResult.Success(value)
    else Error response
        AC->>AC: Deserialize ProblemDetails
        AC->>AC: Map to ApiError (status, code, traceId, ...)
        AC-->>-Cmp: ApiResult.Failure(ApiError)
    end
```

#### Configuration

```json
// appsettings.Development.json (Web)
{
    "ApiClient": {
        "BaseUrl": "https://localhost:7238",
        "TimeoutSeconds": 10
    }
}
```