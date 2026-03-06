# ADR 0019: Field-Level Validation with ICommandValidator

## Status

Accepted

## Context and Problem Statement

Before this change, handlers validated input inline and returned on the first
failure. Two problems arose:

1. **First-error-only**: a client sending a request with both `title` and
   `description` missing received only the title error. It had to submit
   multiple requests to discover all validation failures.

2. **Inconsistent ProblemDetails shape**: `SearchTicketsAsync` produced a
   `ValidationProblemDetails` with an `errors` field (via `Results.ValidationProblem`),
   while all other endpoints produced a flat `ProblemDetails` with a single
   `code` field. Clients had to handle two different 400 shapes.

Additionally, max-length violations surfaced as `DomainViolation` with code
`domain.max_length` — no field name, wrong semantic layer (domain enforcing a
transport constraint).

## Decision Drivers

- A single 400 response must carry all invalid fields at once.
- All 400 validation responses must have the same shape regardless of endpoint.
- Format constraints (required, max-length) belong in the Application layer;
  the Domain's `Guard` is the last line of defence, not the first.
- The solution must compose cleanly with a future MediatR pipeline.

## Considered Options

### Option A — Inline collection per handler

Collect errors in a `Dictionary<string, List<string>>` inside each handler
before returning. No new types.

Simple but spreads the collection logic across every handler with no shared
contract. Harder to wire into a MediatR `IPipelineBehavior` later.

### Option B — ICommandValidator\<TCommand\> (Selected)

Introduce a dedicated interface in the Application layer:

```csharp
public interface ICommandValidator<TCommand>
{
    FieldValidationResult Validate(TCommand command);
}
```

`FieldValidationResult` carries `IsValid` and `IReadOnlyDictionary<string, string[]> FieldErrors`.
`FieldValidationBuilder` provides a fluent API for accumulating errors.
Concrete validators live next to their command in the use-case folder.
Handlers inject `ICommandValidator<TCommand>` and delegate validation before
any domain logic runs.

### Option C — FluentValidation

Industry-standard library with a rich rule DSL. Adds a NuGet dependency and a
learning curve that is not justified for the current scope. Can be introduced
later if the validator count grows significantly.

## Decision Outcome

Chosen option: **Option B — ICommandValidator\<TCommand\>**.

### Why this trade-off makes sense for this project

- **All field errors in one response.** `FieldValidationBuilder.Build()` returns
  a `FieldValidationResult` with every failing field collected, not just the first.
- **Consistent ProblemDetails shape.** `ApiProblemDetailsFactory` detects
  `ApplicationError.FieldErrors` and produces `ValidationProblemDetails` with an
  `errors` object — the same shape previously produced only by `SearchTicketsAsync`.
  `SearchTicketsAsync` is migrated to the same `Result` pipeline.
- **Correct layer ownership.** Required and max-length checks now live in the
  Application validator, not in the Domain. The Domain `Guard` remains as a
  defence-in-depth invariant, not a transport validator.
- **MediatR-ready.** `ICommandValidator<TCommand>` is an Application-layer
  interface with no ASP.NET dependency. A future `ValidationBehavior<TRequest, TResponse>`
  can resolve it from DI without touching the validators themselves.

### Response shape for validation failures

```json
{
  "status": 400,
  "title": "Validation failed",
  "errors": {
    "title": ["Title is required."],
    "description": ["Must not exceed 2000 characters."]
  },
  "code": "create_ticket.validation_failed",
  "errorType": "validation",
  "traceId": "..."
}
```

## Consequences

### Positive Consequences

- Clients receive all field errors in a single round-trip.
- One 400 response shape across all endpoints.
- Validators are independently testable units.
- Handler bodies are free of format-validation noise.

### Negative Consequences

- Each new command that requires validation needs a corresponding validator class
  registered in DI.
- `ICommandValidator<TCommand>` is a custom interface, not a standard one —
  teams unfamiliar with the codebase must learn it.

## Re-evaluation Triggers

Revisit when:

1. MediatR is introduced — migrate to a `ValidationBehavior<TRequest, TResponse>`
   that resolves `ICommandValidator<TRequest>` automatically, removing the
   explicit validator call from each handler.
2. Validation rules grow complex enough to justify FluentValidation's DSL
   (cross-field rules, async validators, localisation).

## Related

- ADR 0002 – Result Pattern as Application Error Contract
- ADR 0003 – RFC 9457 ProblemDetails as the Unified Error Response Contract
- ADR 0004 – Minimal API without MediatR
- `src/ServiceDeskLite.Application/Common/Validation/ICommandValidator.cs`
- `src/ServiceDeskLite.Application/Common/Validation/FieldValidationResult.cs`
- `src/ServiceDeskLite.Application/Common/Validation/FieldValidationBuilder.cs`
- `src/ServiceDeskLite.Application/Common/ApplicationError.cs`
- `src/ServiceDeskLite.Api/Http/ProblemDetails/ApiProblemDetailsFactory.cs`
