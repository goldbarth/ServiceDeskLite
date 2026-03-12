## API Layer (`ServiceDeskLite.Api`)

### Middleware Pipeline Order (`Program.cs`)

```
1.  Serilog configuration (before WebApplication builder)
2.  Services:
      AddApiDocumentation        → OpenAPI + Swagger
      AddApiErrorHandling        → ProblemDetails + ExceptionHandler + Mapper
      AddApplication             → use-case handlers (Scoped)
      AddApiInfrastructure       → persistence provider switch
      AddCors("WebFrontend")     → origins from Cors:AllowedOrigins config
3.  EF Core auto-migration (Postgres only, before app.Build())
4.  app.UseApiRequestLogging()   → Serilog request logging
5.  app.UseApiDocumentation()    → OpenAPI endpoint
6.  app.UseApiErrorHandling()    → UseExceptionHandler()
7.  app.UseApiSecurity()         → ApiKeyMiddleware (X-Api-Key header)
8.  app.UseHttpsRedirection()
9.  app.UseSwagger() / UseSwaggerUI()  → Development only
10. app.UseCors("WebFrontend")
11. Endpoint mapping
```

> In Development, a `ITicketSeeder` runs on startup to populate the InMemory store with sample data.

<img src="../assets/diagrams/middleware-pipeline.svg" alt="Middleware Pipeline" style="max-width:960px;width:100%;">

### Endpoints

**Tickets** (`TicketsEndpoints.cs`) — base route group: `/api/v1/tickets`

| HTTP | Route                                  | Handler                 | Returns                                          |
|------|----------------------------------------|-------------------------|--------------------------------------------------|
| GET  | /api/v1/tickets                        | SearchTicketsAsync      | 200 OK + PagedResponse\<TicketListItemResponse\> |
| POST | /api/v1/tickets                        | CreateTicketAsync       | 201 Created + CreateTicketResponse               |
| GET  | /api/v1/tickets/{id:guid}              | GetTicketByIdAsync      | 200 OK + TicketResponse                          |
| POST | /api/v1/tickets/{id:guid}/status       | ChangeTicketStatusAsync | 200 OK + TicketResponse                          |
| POST | /api/v1/tickets/{id:guid}/comments     | AddCommentAsync         | 201 Created + CommentResponse                    |
| POST | /api/v1/tickets/{id:guid}/assign       | AssignTicketAsync       | 200 OK + TicketResponse                          |
| GET  | /api/v1/tickets/{id:guid}/audit-events | GetAuditEventsAsync     | 200 OK + AuditEventResponse[]                    |

**Dashboard** (`DashboardEndpoints.cs`) — base route group: `/api/v1/dashboard`

| HTTP | Route                     | Handler                  | Returns                           |
|------|---------------------------|--------------------------|-----------------------------------|
| GET  | /api/v1/dashboard/summary | GetDashboardSummaryAsync | 200 OK + DashboardSummaryResponse |

All errors return RFC 9457 ProblemDetails via `ResultToProblemDetailsMapper`.

### Request Lifecycle

The following sequence covers the `POST /api/v1/tickets` happy path. All other endpoints follow the same structure.

<img src="../assets/diagrams/request-lifecycle.svg" alt="Request Lifecycle" style="max-width:960px;width:100%;">

### API Key Authentication (`ApiKeyMiddleware`)

All API endpoints require an `X-Api-Key` header (see ADR 0022).

```csharp
// Slim middleware — runs before all other pipeline stages except logging and error handling.
// Returns 401 Unauthorized (plain, no ProblemDetails body) on missing or invalid key.
// Exempt for OpenAPI/Swagger endpoints in Development.
// Key is read from Auth:ApiKey configuration (injected via environment variable or user secrets).
```

The Blazor Web frontend forwards the key on every outbound request via `ApiKeyDelegatingHandler`.

### Correlation

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

### `ResultToProblemDetailsMapper`

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

### `ResultMappingExtensions`

Fluent bridge: `result.ToHttpResult(ctx, mapper, value => Results.Ok(value.ToResponse()))`.

### Exception Handling Pipeline

- `ApiExceptionHandler : IExceptionHandler` catches all unhandled exceptions
- `ExceptionClassification.IsClientBadRequest(ex)` → `BadHttpRequestException, FormatException, InvalidOperationException` → 400
- `ExceptionClassification.IsCancellation(ex, ctx)` → request cancelled → no response
- All other exceptions → 500 Unexpected

Logging: 5xx → ERROR, 409 → WARNING, 400 → WARNING, others → INFO.

<img src="../assets/diagrams/exception-handling-pipeline.svg" alt="Exception Handling Pipeline" style="max-width:360px;width:100%;">

### Enum Mapping (`Api/Mapping/Tickets/TicketEnumMapping.cs`)

```csharp
// Contracts → Domain
public static DomainTicketPriority ToDomain(this TicketPriority value)
public static DomainTicketStatus   ToDomain(this TicketStatus value)

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

### `appsettings.json` (Production)

```json
{
    "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
    "Persistence": { "Provider": "Postgres" },
    "ConnectionStrings": { "ServiceDeskLite": "<injected via environment variable>" },
    "Auth": { "ApiKey": "<injected via environment variable>" },
    "Cors": { "AllowedOrigins": [] },
    "AllowedHosts": "*"
}
```

### `appsettings.Development.json`

```json
{
    "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
    "Persistence": { "Provider": "InMemory" },
    "Auth": { "ApiKey": "dev-api-key" },
    "Cors": { "AllowedOrigins": [ "https://localhost:7023" ] }
}
```

## OpenAPI Contract Snapshot

The API exposes an OpenAPI v1 specification which is:

- Generated from the running Minimal API
- Snapshotted into `docs/api/openapi.v1.json`
- Rendered via Swagger UI on GitHub Pages
- Verified in CI to detect contract drift

### Purpose

This ensures:

- Contract-driven development
- Transparent API surface for consumers
- Deterministic documentation builds
- Early detection of breaking changes

The OpenAPI snapshot acts as a versioned contract artifact between API and Web layer.
