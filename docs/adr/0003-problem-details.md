# ADR 0003: RFC 9457 ProblemDetails as the Unified Error Response Contract

## Status

Accepted

## Context and Problem Statement

The API needs a consistent shape for every error response — whether the error
originates from a handler result (validation, not-found, conflict) or from an
unhandled exception (malformed request body, unexpected infrastructure fault).

Without a standard, clients must guess the error shape per status code and
maintain brittle parsing logic. The question is: which error format should the
API commit to, and what machine-readable information beyond the HTTP status
should it expose?

## Decision Drivers

- Clients must be able to programmatically identify the specific error
  (`code`) without parsing the human-readable message.
- The error format must be a recognised standard so tooling (OpenAPI, test
  assertions, API clients) can work with it without custom adapters.
- Error responses must include a correlation identifier (`traceId`) to link
  a client-visible error to a server-side log entry.
- Production responses must not leak internal details (stack traces, query
  fragments, internal messages).
- The Web client must be able to deserialise error responses without
  referencing the API project.

## Considered Options

### Option A — RFC 9457 ProblemDetails with custom extension fields (Selected)

Use the standard `application/problem+json` structure (`status`, `title`,
`instance`) and extend it with four additional fields: `code` (machine-readable
error discriminator), `errorType` (category string), `traceId`, and optionally
`meta` (structured extra context). Extension key names are constants in
`Contracts`, shared by both the API and the Web client.

### Option B — Custom JSON error envelope

Define a project-specific error shape (e.g.
`{ "error": { "code": "...", "message": "..." } }`). Full control over the
structure, but no standard tooling support and no shared vocabulary with the
HTTP ecosystem.

### Option C — HTTP status codes only, no body

Return only a status code for errors. Zero implementation overhead, but
forces clients to treat every 400 the same regardless of root cause, and
provides nothing for support to trace an error back to a log entry.

## Decision Outcome

Chosen option: **Option A — RFC 9457 ProblemDetails with custom extension fields**.

### Why this trade-off makes sense for this project

- **The standard buys interoperability for free.** ASP.NET Core, OpenAPI
  tooling, and test libraries all understand `application/problem+json`. Using
  it means less glue code everywhere.
- **`code` carries the machine-readable discriminator, not `Detail`.**
  `Detail` is intentionally `null` in production to avoid leaking internal
  information. `code` (e.g. `"domain.ticket.status.invalid_transition"`) gives
  the caller a stable, parseable identifier without revealing internals.
- **The extension keys live in `Contracts`, not in `Api`.** `ProblemDetailsContract`
  defines the field names (`code`, `errorType`, `traceId`, `meta`) as
  constants. Both the API and the Web client reference this class — the Web
  client never needs to reference the API project to parse an error response.
- **The format is fully unified.** Handler errors (via `ResultToProblemDetailsMapper`)
  and unhandled exceptions (via `ApiExceptionHandler`) both flow through the
  same `ApiProblemDetailsFactory`. There is no secondary error format anywhere
  in the API surface.

### Response shape

```json
{
  "status": 400,
  "title": "Validation failed",
  "instance": "/api/v1/tickets",
  "code": "create_ticket.title.required",
  "errorType": "validation",
  "traceId": "00-a1b2c3...-01"
}
```

The `meta` field is included only when the error carries structured context
(e.g. field names with their validation messages). It is omitted otherwise.

## Consequences

### Positive Consequences

- Every error response has the same shape — clients need one parser.
- `code` gives a stable, version-safe discriminator for programmatic
  branching without coupling to HTTP status nuances.
- `traceId` directly links a user-visible error to the corresponding
  structured log entry.
- OpenAPI schema generation can declare the error shape once and reference
  it across all endpoints.

### Negative Consequences

- `Detail = null` in production means human-readable context is not
  available in the response body. The `code` field must be informative
  enough for a developer to identify the problem without server log access.
- Adding a new error category requires updating `ProblemDetailsContract`
  (in `Contracts`), `ErrorType` (in `Application`), and the HTTP mapping
  switch in both `ResultToProblemDetailsMapper` and `ApiExceptionHandler`.

## Re-evaluation Triggers

Revisit when:

1. A consumer needs richer machine-readable context than `code` + `meta`
   can provide (candidate: JSON:API error objects).
2. The API introduces versioning and the error shape needs to evolve
   independently per version.

## Related

- ADR 0002 – Result Pattern as Application Error Contract
- ADR 0009 – Versioned HTTP Contracts in a dedicated `Contracts` project
- `src/ServiceDeskLite.Contracts/V1/Common/ProblemDetailsContract.cs`
- `src/ServiceDeskLite.Api/Http/ProblemDetails/ApiProblemDetailsFactory.cs`
- `src/ServiceDeskLite.Api/Http/ProblemDetails/ApiProblemDetailsConventions.cs`
- `src/ServiceDeskLite.Api/Http/ProblemDetails/ResultToProblemDetailsMapper.cs`
- `src/ServiceDeskLite.Api/Http/ExceptionHandling/ApiExceptionHandler.cs`
- `src/ServiceDeskLite.Web/Api/V1/ProblemDetailsDto.cs`
