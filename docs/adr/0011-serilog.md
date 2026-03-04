# ADR 0011: Serilog as the Structured Logging Library

## Status

Accepted

## Context and Problem Statement

ASP.NET Core ships with `Microsoft.Extensions.Logging` (MEL) and a set of
built-in providers (Console, Debug, EventSource). These are sufficient for
basic logging but produce unstructured text output by default and offer
limited control over output format, enrichment, and sink configuration.

The question is whether the built-in providers are adequate for this project,
or whether a structured logging library adds enough value to justify an
additional dependency.

## Decision Drivers

- Log output must include stable, machine-readable context fields
  (machine name, thread ID, correlation trace ID) alongside the message,
  so that log entries can be correlated across requests without text parsing.
- Startup failures — exceptions thrown before the DI container is ready —
  must be captured and logged, not silently lost.
- Request logging (method, path, status code, elapsed time) should be
  handled by a single middleware rather than scattered across endpoints.
- The logging library must integrate cleanly with `ILogger<T>` so that
  application and infrastructure code remains decoupled from the Serilog
  API.

## Considered Options

### Option A — Serilog with Console sink and structured enrichers (Selected)

Serilog replaces the built-in MEL providers. A bootstrap `Log.Logger` is
configured before `WebApplication.CreateBuilder` to capture startup
failures. After the host is built, `builder.Host.UseSerilog(...)` installs
the full configured logger. `app.UseSerilogRequestLogging()` handles
per-request log entries. All application code uses the MEL `ILogger<T>`
abstraction — Serilog is only referenced in `Program.cs`.

### Option B — Built-in MEL Console provider only

No additional dependency. Sufficient for simple scenarios, but the Console
output is unstructured text. Enriching every log entry with machine name
and thread ID requires custom middleware or manual log-scope setup.
Startup failures before the host is built are not captured.

### Option C — OpenTelemetry logging

A standards-based approach that integrates with distributed tracing
infrastructure. Appropriate when logs need to be exported to a collector
(Jaeger, Grafana Loki, etc.), but introduces significant configuration
overhead for a project that currently targets Console output only.

## Decision Outcome

Chosen option: **Option A — Serilog with Console sink and structured enrichers**.

### Why this trade-off makes sense for this project

- **Two-stage initialisation captures startup failures.** A bootstrap
  `Log.Logger` is created before the builder, using the same enrichers and
  Console sink. If the host fails to start (e.g. invalid configuration,
  port conflict), the exception is written to the log before the process
  exits. After the host is built, `UseSerilog` replaces the bootstrap logger
  with the fully configured instance.
- **Enrichers add correlation context without touching application code.**
  `WithMachineName()` and `WithThreadId()` attach context to every log
  entry automatically. `FromLogContext()` picks up any properties pushed
  via `ILogger.BeginScope(...)`, including the `traceId` and `code` fields
  added by `ApiExceptionHandler` for every error response.
- **`UseSerilogRequestLogging()` replaces the default ASP.NET Core access
  log.** The default access log produces one unstructured line per request.
  Serilog's middleware produces a structured entry with method, path, status
  code, and elapsed time as separate properties — filterable and queryable
  without string parsing.
- **Application code stays decoupled from Serilog.** Only `Program.cs`
  references `Serilog.*` directly. Every other layer uses `ILogger<T>` from
  `Microsoft.Extensions.Logging`. Replacing Serilog in the future requires
  changing only the composition root.
- **Stable `EventId` values allow filtering without text matching.**
  `LogEvents` defines named `EventId` constants for known error categories
  (`ApiError = 1000`, `ApiConflict = 1001`, `ApiValidation = 1002`,
  `ApiNotFound = 1003`). Log aggregation tools can filter on `EventId`
  rather than parsing message text.

## Consequences

### Positive Consequences

- Startup exceptions are captured and visible in the log, not silently lost.
- Every log entry carries machine name and thread ID without any per-callsite
  effort.
- Switching to a structured sink (Seq, Grafana Loki, Elastic) in a later
  milestone requires only adding a sink package and updating `Program.cs`.
- The request log is a single structured entry per request, not a mix of
  framework noise.

### Negative Consequences

- Two-stage initialisation means some configuration is written twice
  (enrichers and sink repeated in both the bootstrap logger and
  `UseSerilog`). This is a known Serilog pattern but adds minor maintenance
  overhead.
- Serilog is a hard dependency in `Program.cs`. If the project ever needs
  to run in an environment where Serilog packages are not available, the
  composition root must be changed.

## Re-evaluation Triggers

Revisit when:

1. Logs need to be exported to a centralised sink or collector — add the
   appropriate Serilog sink package (e.g. `Serilog.Sinks.OpenTelemetry`)
   without changing application code.
2. OpenTelemetry distributed tracing is introduced — evaluate whether Serilog
   and OpenTelemetry logging should be unified under a single pipeline.

## Related

- `src/ServiceDeskLite.Api/Program.cs` — bootstrap logger and `UseSerilog`
- `src/ServiceDeskLite.Api/Http/Observability/LogEvents.cs`
- `src/ServiceDeskLite.Api/Http/Observability/Correlation.cs`
- `src/ServiceDeskLite.Api/Http/ExceptionHandling/ApiExceptionHandler.cs` — `BeginScope` with `traceId` and `code`
