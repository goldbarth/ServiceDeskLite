# ADR 0002: Result Pattern as Application Error Contract

## Status

Accepted

## Context and Problem Statement

Use-case handlers need a way to communicate outcomes — success, validation
errors, business rule violations, not-found cases — to the layer above them.
The most common alternative is throwing exceptions for every non-happy-path
case, which the calling code then catches and inspects.

The question is: should handler outcomes be communicated through exceptions, or
through explicit return values?

## Decision Drivers

- The application layer must not surprise callers with unexpected exception types.
- Error cases (validation, not-found, domain violation) are expected outcomes,
  not exceptional ones — they should not require exception handling at the call site.
- The `ErrorType` must carry enough information for the API layer to map it to
  the correct HTTP status code — without the API layer needing to know about
  domain internals.
- `OperationCanceledException` must never be silently swallowed.

## Considered Options

### Option A — Explicit Result type (Selected)

Handlers return `Result` (void success) or `Result<T>` (value on success).
Every failure carries an `ApplicationError` with a machine-readable `code`,
a human-readable `message`, and an `ErrorType` that drives HTTP status mapping.
`DomainException` is caught inside the handler and converted to
`Result.DomainViolation(...)`. `OperationCanceledException` is explicitly
not caught and propagates to the infrastructure layer.

### Option B — Exceptions as the error channel

Handlers throw typed exceptions (`NotFoundException`, `ValidationException`,
etc.) for expected failures. The API layer catches them via middleware or
exception filters and maps them to HTTP responses.

This means expected business outcomes travel as exceptions — making every
call site an implicit try/catch consumer and coupling the API error-handling
middleware to application exception types.

### Option C — Tuple or out-parameter return

Handlers return `(T? value, Error? error)` tuples. Avoids exceptions but
provides no compile-time enforcement that callers check the error, and no
structured classification of error types.

## Decision Outcome

Chosen option: **Option A — Explicit Result type**.

### Why this trade-off makes sense for this project

- **Expected failures are not exceptions.** A ticket not found, a title
  missing, an invalid status transition — these are defined outcomes of a
  use case, not infrastructure failures. Returning them as values makes the
  contract explicit and readable at the call site.
- **`DomainException` is caught exactly once, at the handler boundary.**
  The domain layer uses exceptions to enforce invariants (that is its job).
  The application handler catches them, converts them via
  `DomainExceptionMapper`, and returns a `Result.DomainViolation(...)`.
  After this point, no domain exception reaches the HTTP layer.
- **`OperationCanceledException` is never caught.** Request cancellations
  are not application errors. They are handled separately by the API
  exception handler (`ExceptionClassification.IsCancellation`).
- **`DomainViolation` maps to HTTP 400, not 422 or 500.** Domain rule
  violations (e.g., invalid status transition) are caused by bad client
  input, not by server-side faults. 400 is the appropriate signal to the
  caller that the request was semantically wrong. 422 would require
  `Content-Type: application/problem+json` negotiation overhead not
  justified here; 500 would be factually incorrect.

### ErrorType → HTTP status mapping

| ErrorType         | HTTP Status |
|-------------------|-------------|
| `Validation`      | 400         |
| `DomainViolation` | 400         |
| `NotFound`        | 404         |
| `Conflict`        | 409         |
| `Unexpected`      | 500         |

## Consequences

### Positive Consequences

- Handler signatures are self-documenting: the return type communicates that
  failure is a possible outcome.
- The API layer needs only a single `ResultToProblemDetailsMapper` — no
  middleware catch-chain for typed exceptions.
- Handlers are straightforward to test: assert `result.IsSuccess`,
  `result.Error.Type`, and `result.Error.Code` without try/catch in tests.

### Negative Consequences

- Handlers must explicitly return a failure result for every error case —
  no implicit propagation via throw.
- `DomainException` must be caught in every handler that calls domain
  methods. This is repetitive but intentional: each handler decides how to
  present domain failures to its caller.

## Re-evaluation Triggers

Revisit when:

1. A cross-cutting concern (e.g., logging, auth) needs to intercept every
   handler outcome uniformly — a pipeline with `IPipelineBehavior` (MediatR)
   might reduce the per-handler catch repetition.
2. The number of `ErrorType` variants grows to the point where the HTTP
   mapping table becomes hard to maintain.

## Related

- ADR 0001 – Clean / Hexagonal Layered Architecture
- ADR 0003 – RFC 9457 ProblemDetails as the Unified Error Response Contract
- `src/ServiceDeskLite.Application/Common/Result.cs`
- `src/ServiceDeskLite.Application/Common/ResultOfT.cs`
- `src/ServiceDeskLite.Application/Common/ApplicationError.cs`
- `src/ServiceDeskLite.Application/Common/DomainExceptionMapper.cs`
- `src/ServiceDeskLite.Api/Http/ProblemDetails/ResultToProblemDetailsMapper.cs`
