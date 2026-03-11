# ADR 0021: Outbox Pattern – Architectural Stub

## Status

Accepted (Showcase Stub)

## Context and Problem Statement

ADR 0008 identified the Outbox pattern as the next maturity step once the
Unit-of-Work boundary needs to guarantee reliable event publication to an
external consumer. Without an outbox, any approach that publishes events
*after* `SaveChangesAsync` has a window in which the write succeeds but the
event is never published — e.g. if the process crashes between the two
operations.

The project currently has no external event bus and does not need one.
However, leaving the architectural path entirely implicit makes it harder
for future contributors to understand how to extend the system. The goal
here is to demonstrate *where* the outbox fits, not to build a production
dispatcher.

## Decision Drivers

- Make the reliability gap between "audit record" and "reliable external
  publication" tangible in code.
- Show the correct layer placement without introducing a full event bus.
- Keep the stub clearly labelled so it is never mistaken for production-ready
  infrastructure.

## Decision Outcome

A **minimal outbox stub** is introduced at the correct architectural layers.
It is explicitly documented as a showcase — no background dispatcher, no
retry logic, no dead-letter handling.

### What was built

| Layer | Artifact | Purpose |
|---|---|---|
| Domain | `OutboxMessage`, `OutboxMessageId`, `OutboxMessageStatus` | Entity that holds the pending event |
| Application | `IOutboxRepository`, `OutboxMessageFactory` | Port + translation from domain event |
| Application | `CreateTicketHandler` (updated) | Writes ticket + audit + outbox atomically |
| Infrastructure | `EfOutboxRepository`, `OutboxMessageConfiguration`, migration | EF Core / PostgreSQL persistence |
| Infrastructure.InMemory | `InMemoryOutboxRepository` | In-memory implementation for tests |

### Atomic write guarantee

All three writes (ticket, audit event, outbox message) are staged before the
single `SaveChangesAsync` call. This is the core invariant: either all three
are durable, or none of them are. There is no window in which a ticket exists
without a corresponding outbox message.

```
await _repository.AddAsync(ticket, ct);
await _auditRepository.AddAsync(auditEvent, ct);    // ← audit
await _outboxRepository.AddAsync(outboxMessage, ct); // ← outbox
ticket.ClearDomainEvents();
await _unitOfWork.SaveChangesAsync(ct);              // ← single commit
```

### What is intentionally missing

The following components belong to a production Outbox implementation but
are **not part of this stub**:

- Background dispatcher / hosted service that polls for `Pending` messages
- `GetPendingAsync` on `IOutboxRepository` (would be a separate dispatcher port)
- Retry counters, error columns, lock/lease mechanism
- Dead-letter queue or permanent-failure handling
- Integration with an actual message broker (RabbitMQ, Azure Service Bus, etc.)

### Why `DispatchedAt` instead of a stored status column

`OutboxMessageStatus` is computed from `DispatchedAt` in the domain model
and is explicitly ignored by EF Core (`builder.Ignore(m => m.Status)`).
This avoids two columns that could drift out of sync. There is one source of
truth: the presence or absence of a timestamp.

### Schema

```sql
CREATE TABLE "OutboxMessages" (
    "Id"           uuid                     NOT NULL,
    "EventType"    character varying(100)   NOT NULL,
    "Payload"      text                     NOT NULL,
    "OccurredAt"   timestamp with time zone NOT NULL,
    "DispatchedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_OutboxMessages" PRIMARY KEY ("Id")
);
CREATE INDEX "IX_OutboxMessages_OccurredAt"   ON "OutboxMessages" ("OccurredAt");
CREATE INDEX "IX_OutboxMessages_DispatchedAt" ON "OutboxMessages" ("DispatchedAt");
```

`DispatchedAt IS NULL` is the efficient filter for pending messages.
PostgreSQL handles nullable-column indexes well for this pattern.

## Consequences

### Positive Consequences

- The architectural slot for reliable event publication is now visible and
  navigable in code, not just described in an ADR.
- The atomic write guarantee is tested via `InMemoryOutboxRepositoryTests`.
- A future contributor can add a dispatcher by implementing a hosted service
  that reads `WHERE DispatchedAt IS NULL` and calls `MarkDispatched` — no
  schema changes needed.

### Negative Consequences

- `OutboxMessages` rows accumulate indefinitely until a dispatcher clears
  them. In production, a cleanup/archival strategy would be required.
- `CreateTicketHandler` now has five dependencies. This is acceptable for a
  showcase but is a signal that a future refactor (e.g. an event-dispatch
  pipeline) could consolidate audit + outbox staging.

## Re-evaluation Triggers

Revisit when:

1. An external consumer (message broker, webhook) is introduced — that is the
   point at which the stub must become a real dispatcher.
2. Additional write use cases need to publish events — at that point a
   cross-cutting pipeline (e.g. a post-commit hook in `IUnitOfWork`) may be
   preferable to adding `IOutboxRepository` to every handler.

## Related

- ADR 0008 – Unit-of-Work Pattern (identifies outbox as re-evaluation trigger)
- `src/ServiceDeskLite.Domain/Outbox/OutboxMessage.cs`
- `src/ServiceDeskLite.Application/Abstractions/Persistence/IOutboxRepository.cs`
- `src/ServiceDeskLite.Application/Tickets/Outbox/OutboxMessageFactory.cs`
- `src/ServiceDeskLite.Application/Tickets/CreateTicket/CreateTicketHandler.cs`
- `src/ServiceDeskLite.Infrastructure/Persistence/Migrations/*_AddOutboxMessages.cs`
- `tests/ServiceDeskLite.Tests.Infrastructure.InMemory/InMemoryOutboxRepositoryTests.cs`