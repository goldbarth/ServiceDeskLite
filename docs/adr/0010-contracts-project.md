# ADR 0010: Versioned HTTP Contracts in a Dedicated `Contracts` Project

## Status

Accepted

## Context and Problem Statement

The API exposes HTTP request and response types that must be understood by
both the API host and the Web client. The question is where these shared
types should live and how they should be versioned.

If the types live inside the API project, any consumer that needs them must
reference the API project — pulling in ASP.NET Core middleware, EF Core,
Serilog, and every other dependency the API carries. That is not a contract
boundary; it is the entire API implementation.

## Decision Drivers

- The Web client must be able to use the shared DTO types without referencing
  the API project or any of its infrastructure dependencies.
- Breaking changes to the HTTP contract must be identifiable by namespace:
  a type in `V1` is part of the v1 contract; a type in `V2` is not.
- Enum values in JSON responses must be human-readable strings, not integers,
  so that API responses are self-describing without a schema lookup.
- The `Contracts` project must carry zero framework dependencies — it should
  be a plain .NET class library that any consumer can reference cheaply.

## Considered Options

### Option A — Dedicated `Contracts` project with `V1/` namespace versioning (Selected)

All HTTP request/response types, shared enums, and the ProblemDetails
extension-key constants live in `ServiceDeskLite.Contracts` under the
`V1/` namespace. Both `Api` and `Web` reference `Contracts`.
`Api` maps between Contracts types and Application/Domain types at the
endpoint boundary. `Web` uses Contracts types directly as the API client's
input and output types.

### Option B — DTOs colocated with the API project

Request and response types live inside `ServiceDeskLite.Api`. The Web
project adds a `ProjectReference` to the API project to share them.
This is the path of least resistance but makes the Web project a consumer
of all API infrastructure — violating the principle that the Web should
only depend on the public HTTP contract, not on its implementation.

### Option C — Shared types in the Application layer

Place DTOs in `Application` alongside use-case types. Eliminates a project
but conflates two responsibilities: application orchestration types and
HTTP wire types. The Application layer would then need to model HTTP
concerns (pagination response shape, string-serialised enums), which is
outside its scope. The Web project would also pull in application-layer
types it does not need.

## Decision Outcome

Chosen option: **Option A — Dedicated `Contracts` project with `V1/` namespace versioning**.

### Why this trade-off makes sense for this project

- **The dependency graph stays clean.** `Contracts` has zero `ProjectReference`
  and zero framework `PackageReference` entries — it is a pure type library.
  Both `Api` and `Web` reference it; neither references the other.
- **Namespace versioning makes breaking changes explicit.** A type under
  `ServiceDeskLite.Contracts.V1` is part of the v1 contract. When v2 is
  introduced, new types go under `V2/`. Existing v1 consumers are unaffected
  until they explicitly migrate.
- **String enums are enforced at the API boundary, not in `Contracts`.**
  `JsonStringEnumConverter` is registered in the API's JSON options, so all
  responses serialise enum values as strings at runtime. The Web client
  registers the same converter in its outbound `_sendOptions` to ensure
  requests serialize identically. `Contracts` itself does not depend on
  `System.Text.Json` — it defines the types; the serialisation policy is
  the host's responsibility.
- **`ProblemDetailsContract` also lives in `Contracts`.** The extension key
  constants (`code`, `errorType`, `traceId`, `meta`) and the `ErrorTypes`
  string values are defined here so both the API factory and the Web client
  can reference them without a circular dependency. This is covered in detail
  in ADR 0003.

### Project reference graph (simplified)

```
Web  ──► Contracts
Api  ──► Contracts
Api  ──► Application
Api  ──► Domain
Api  ──► Infrastructure
Api  ──► Infrastructure.InMemory
```

## Consequences

### Positive Consequences

- Adding a v2 endpoint family is a namespace addition, not a project
  restructuring.
- The Web project remains lightweight: it depends on `Contracts` (pure
  types) and MudBlazor, not on any server-side infrastructure.
- The OpenAPI schema reflects the Contracts types directly, giving the
  generated spec a stable, independently versionable contract surface.

### Negative Consequences

- Every feature that adds or changes an HTTP type requires a change in
  `Contracts` in addition to the `Application` and `Api` layers — an extra
  file per new endpoint.
- The mapping between `Contracts` types and `Application`/`Domain` types
  (in `Api/Mapping/`) must be maintained manually. There is no automatic
  sync between the two models.

## Re-evaluation Triggers

Revisit when:

1. A `V2` API surface is introduced — confirm the namespace strategy holds
   and that v1 consumers are not inadvertently broken.

## Related

- ADR 0001 – Clean / Hexagonal Layered Architecture
- ADR 0003 – RFC 9457 ProblemDetails as the Unified Error Response Contract
- `src/ServiceDeskLite.Contracts/ServiceDeskLite.Contracts.csproj`
- `src/ServiceDeskLite.Contracts/V1/Common/ProblemDetailsContract.cs`
- `src/ServiceDeskLite.Web/ServiceDeskLite.Web.csproj`
- `src/ServiceDeskLite.Api/Mapping/Tickets/TicketEnumMapping.cs`
- `src/ServiceDeskLite.Web/Api/V1/TicketsApiClient.cs`
