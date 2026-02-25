## Web Layer (`ServiceDeskLite.Web`)

#### API Client

```csharp
// ServiceDeskLite.Web.Api.V1
public interface ITicketsApiClient
{
    Task<ApiResult<PagedResponse<TicketListItemResponse>>> SearchAsync(
        SearchTicketsRequest request, CancellationToken ct = default);

    Task<ApiResult<TicketResponse>> GetByIdAsync(
        Guid id, CancellationToken ct = default);

    Task<ApiResult<CreateTicketResponse>> CreateAsync(
        CreateTicketRequest request, CancellationToken ct = default);

    Task<ApiResult<TicketResponse>> ChangeStatusAsync(
        Guid id, ChangeTicketStatusRequest request, CancellationToken ct = default);
}
```

`TicketsApiClient` uses `HttpClient` with `PropertyNameCaseInsensitive` JSON deserialization and parses ProblemDetails from API error responses.
Outgoing requests that carry enum values (e.g., `ChangeStatusAsync`) are serialised with camelCase + `JsonStringEnumConverter`.

#### `ApiResult<T>` and `ApiError`

```csharp
public sealed class ApiResult<T>
{
    public bool IsSuccess => Error is null;
    public T? Value { get; }
    public ApiError? Error { get; }

    public static ApiResult<T> Success(T value)
    public static ApiResult<T> Failure(ApiError? error)
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

#### `ProblemDetailsDto`

Internal DTO used to deserialise RFC 9457 error responses from the API before mapping them to `ApiError`.

```csharp
public class ProblemDetailsDto
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int? Status { get; init; }
    public string? Detail { get; init; }
    public string? Instance { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; init; }
}
```

`[JsonExtensionData]` captures all extra fields (`code`, `errorType`, `traceId`, `meta`) without requiring an explicit property per extension key.

#### Client / API Result Types

![Client / API Result Types](../assets/diagrams/client-api-result-types.svg)

#### API Client Call Flow

![API Client Call Flow](../assets/diagrams/api-client-call-flow.svg)

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
