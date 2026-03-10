# ADR 0020: AuditEvent Hybrid Payload Format

## Status

Accepted

## Context and Problem Statement

Milestone 2 introduces an append-only audit log that records every significant
state change on a `Ticket`: creation, status transitions, assignee changes, and
comments. Each `AuditEvent` must be queryable by ticket and time range, but the
data that makes sense to store per event varies significantly by event type.

The central tension is between two competing needs:

- **Query efficiency.** Fields used in `WHERE` and `ORDER BY` clauses
  (ticket reference, timestamp, event type) benefit from being first-class
  typed columns with indexes.
- **Schema flexibility.** Event-specific details vary in shape per event type
  and will grow as new event types are introduced. Forcing every possible field
  into a fixed schema creates either sparse rows or a migration every time a
  new event type ships.

An additional constraint: persistence is SQLite in Milestone 2 and PostgreSQL
(via Dapper) in Milestone 3. The storage representation must work for both
without requiring changes to the Domain or Application layers.

## Decision Drivers

- Audit events are append-only — they are never updated after insert.
- The primary read path is "all events for a ticket, ordered by time" — no
  cross-ticket payload queries are expected in Milestone 2.
- New event types must not require a schema migration.
- The Domain and Application layers must have no knowledge of JSON or any
  serialisation format.
- The Web UI must be able to render event-specific details in a type-safe way
  without parsing raw JSON strings.

## Considered Options

### Option A — Fully typed columns per event type

Each event type gets its own table or a set of nullable columns representing
its payload fields. For example: `StatusChangedFrom`, `StatusChangedTo`,
`NewAssignee`, `PreviousAssignee`, `CommentAuthor`, `CommentContent`.

### Option B — Hybrid model: typed core fields + JSON payload (Selected)

Fields used in queries are normal typed columns. Event-specific data is stored
as a JSON string (`TEXT` in SQLite, `JSONB` in PostgreSQL). A single
`AuditEvents` table covers all event types.

### Option C — Pure document store / event store

All fields including the core ones are embedded in a single JSON document per
event. No relational columns at all. Requires a document-capable store or
significant filtering overhead when querying by ticket or time range.

## Decision Outcome

Chosen option: **Option B — Hybrid model with typed core fields and JSON payload**.

### Typed core fields

The following fields are mapped to regular columns with indexes:

| Field        | Type             | Index | Reason                                    |
|--------------|------------------|-------|-------------------------------------------|
| `Id`         | `Guid`           | PK    | Unique identity                           |
| `TicketId`   | `Guid`           | Yes   | Primary read filter                       |
| `EventType`  | `string` (≤ 100) | —     | Grouping, display, payload discrimination |
| `Actor`      | `string` (≤ 200) | —     | Who performed the action                  |
| `OccurredAt` | `DateTimeOffset` | Yes   | Chronological ordering                    |

### JSON payload

Event-specific data is stored as a camelCase JSON string in a `Payload` column
(`TEXT` in SQLite, `JSONB` in PostgreSQL). The `AuditEventFactory` in the
Application layer is the single point of serialisation — the Domain layer never
touches JSON.

Current payload shapes:

```json lines
// ticket.created
{ "title": "Login page broken", "priority": "High" }

// ticket.status_changed
{ "fromStatus": "New", "toStatus": "Triaged" }

// ticket.assignee_changed
{ "previousAssignee": "alice", "newAssignee": "bob" }

// ticket.comment_added
{ "author": "alice", "content": "Reproduced on Firefox 124." }
```

### Typed Contracts payload hierarchy

Although the payload is stored and transported as a JSON string, the Web UI
must not parse it directly. The `Contracts` layer defines a typed
`AuditEventPayload` hierarchy using `System.Text.Json`'s `[JsonPolymorphic]`
and `[JsonDerivedType]` attributes:

```
AuditEventPayload (abstract record, base type)
├── TicketCreatedPayload           (Title, Priority)
├── TicketStatusChangedPayload     (FromStatus, ToStatus)
├── TicketAssigneeChangedPayload   (PreviousAssignee?, NewAssignee?)
├── TicketCommentAddedPayload      (Author?, Content)
└── RawAuditEventPayload           (RawJson) — catch-all for unknown types
```

The API layer deserialises the stored JSON string into the appropriate subtype
(keyed on `EventType`) and serialises the typed object to the HTTP response.
The `$type` discriminator written by `[JsonPolymorphic]` allows the Web client
to deserialise directly into the correct concrete type — no `EventType` string
comparisons or `JsonSerializer` calls in the UI.

### Why this trade-off makes sense for this project

- **Query path is unaffected.** "All events for ticket X, ordered by
  `OccurredAt`" touches only indexed typed columns — the payload is never
  read by the database during the primary read.
- **New event types are additive.** Introducing `TicketEscalated` or
  `PriorityChanged` requires adding a factory method, a new `[JsonDerivedType]`
  in Contracts, and a branch in the API mapper — no schema migration.
- **PostgreSQL JSONB is a drop-in upgrade.** Changing the column type from
  `TEXT` to `JSONB` in Milestone 3 unlocks `->>`-style queries and GIN
  indexes without touching the Domain or Application layers.
- **The Web UI stays a pure consumer.** `[JsonPolymorphic]` is resolved
  automatically by both the API serialiser and the Web client's deserialiser
  because both reference the same `Contracts` assembly. The Razor template
  uses `is`-pattern matching on payload subtypes — no raw JSON, no string
  comparisons.

## Consequences

### Positive Consequences

- The primary read path (`GetByTicketId`, ordered by `OccurredAt`) is
  efficient: indexed columns, no JSON parsing by the database.
- New event types ship without schema migrations in either persistence
  provider.
- The `Contracts` payload hierarchy enforces type-safe rendering in the Web UI
  and will catch broken payload shapes at compile time when Contracts change.
- The `RawAuditEventPayload` catch-all ensures the UI degrades gracefully
  for event types not yet registered in Contracts.

### Negative Consequences

- The payload shape per event type is not enforced at the database level — a
  malformed JSON string would only surface at read time.
- Payload validation is the Application layer's responsibility (enforced via
  `AuditEventFactory`), not the persistence layer's.
- Cross-event payload queries (e.g. "all status changes to `Closed`") require
  JSON functions in SQLite (limited) or `JSONB` operators in PostgreSQL — not
  available until Milestone 3.
- Adding a new event type requires coordinated changes in four places:
  `AuditEventFactory`, `AuditEventTypes`, `AuditEventMapping`, and the
  `[JsonDerivedType]` registration in `AuditEventPayload`.

## Re-evaluation Triggers

Revisit when:

1. **Milestone 3 (PostgreSQL / Dapper) is implemented** — confirm the `Payload`
   column migrates to `JSONB` and that no Application-layer changes are needed.
2. **Cross-payload queries are required** — if reporting or analytics need to
   filter or aggregate by payload fields, consider materialising those fields
   into typed columns or a read-model projection.
3. **A new event type is introduced** — verify the four-file change set is
   complete and covered by a test in `AuditEventMappingTests`.

## Related

- ADR 0001 – Clean / Hexagonal Layered Architecture
- ADR 0007 – Swappable Persistence via Runtime Provider Switch
- ADR 0010 – Versioned HTTP Contracts in a Dedicated `Contracts` Project
- `src/ServiceDeskLite.Domain/Audit/AuditEvent.cs`
- `src/ServiceDeskLite.Domain/Audit/AuditEventTypes.cs`
- `src/ServiceDeskLite.Application/Tickets/Audit/AuditEventFactory.cs`
- `src/ServiceDeskLite.Contracts/V1/Tickets/AuditEventPayload.cs`
- `src/ServiceDeskLite.Contracts/V1/Tickets/AuditEventResponse.cs`
- `src/ServiceDeskLite.Api/Mapping/Tickets/AuditEventMapping.cs`
- `src/ServiceDeskLite.Infrastructure/Persistence/Configurations/AuditEventConfiguration.cs`
- `tests/ServiceDeskLite.Tests.Api/Tickets/AuditEventMappingTests.cs`
