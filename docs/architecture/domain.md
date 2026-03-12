## Domain Layer (`ServiceDeskLite.Domain`)

### `Ticket` Aggregate Root

```csharp
public sealed class Ticket
{
    public TicketId Id { get; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? DueAt { get; private set; }
    public Assignee? Assignee { get; private set; }

    public IReadOnlyList<Comment> Comments { get; }
    public IReadOnlyList<IDomainEvent> DomainEvents { get; }
    public void ClearDomainEvents()

    public Ticket(TicketId id, string title, string description,
                  TicketPriority priority, DateTimeOffset createdAt,
                  DateTimeOffset? dueAt = null)   // Guard validates all inputs

    public void ChangeStatus(TicketStatus newStatus)  // Delegates to TicketWorkflow
    public void Assign(Assignee? assignee)            // Raises AssigneeChangedDomainEvent
    public Comment AddComment(string content, DateTimeOffset createdAt,
                              string? author = null)  // Raises CommentAddedDomainEvent
}
```

`Ticket` is constructed via its public constructor. Guard validation runs
inside the constructor. EF Core uses a private parameterless constructor for
materialization; it is not visible externally.

Every mutation raises a domain event (see below). Handlers consume these
events after the mutation to produce audit records and outbox messages.

### `TicketId` – Strongly-Typed ID

```csharp
public readonly record struct TicketId(Guid Value)
{
    public static TicketId New() => new(Guid.CreateVersion7());
}
```

All entity IDs in this project follow the same `readonly record struct`
pattern. `Guid.CreateVersion7()` produces time-ordered GUIDs, which are
favourable for database index locality.

### Enums

```csharp
public enum TicketStatus   { New, Triaged, InProgress, Waiting, Resolved, Closed }
public enum TicketPriority { Low, Medium, High, Critical }
```

### Value Objects

#### `Assignee`

```csharp
public sealed record Assignee(string Name);
```

Stored as a single `character varying(100)` column in the `Tickets` table.
`null` means unassigned.

#### `Comment` + `CommentId`

```csharp
public readonly record struct CommentId(Guid Value)
{
    public static CommentId New() => new(Guid.CreateVersion7());
}

public sealed class Comment
{
    public CommentId Id { get; }
    public string Content { get; }
    public DateTimeOffset CreatedAt { get; }
    public string? Author { get; }
}
```

Comments are owned by the `Ticket` aggregate and stored in a separate
`TicketComments` table with a FK to `Tickets`.

### Domain Model

<img src="../assets/diagrams/domain-model.svg" alt="Domain Model" style="max-width:960	px;width:100%;">

### `TicketWorkflow` – Status Transition Rules

```csharp
private static readonly HashSet<(TicketStatus From, TicketStatus To)> _allowed =
[
    (New,        Triaged),
    (Triaged,    InProgress),
    (Triaged,    Waiting),
    (Triaged,    Resolved),
    (InProgress, Waiting),
    (InProgress, Resolved),
    (Waiting,    InProgress),
    (Waiting,    Resolved),
    (Resolved,   Closed),
    (Resolved,   InProgress),   // Reopen path
];

public static bool CanTransition(TicketStatus from, TicketStatus to)
public static void EnsureCanTransition(TicketStatus from, TicketStatus to)
// Throws DomainException with code "domain.ticket.status.invalid_transition"
```

### Status Transition Diagram

## Ticket Workflow

<img src="../assets/diagrams/ticket-workflow.svg" alt="Ticket Workflow" style="max-width:360px;width:100%;">

### Domain Events

Every aggregate mutation raises a domain event implementing `IDomainEvent`.
Events are collected on the aggregate (`DomainEvents` list) and consumed by
the Application handler after the mutation — the Domain layer has no knowledge
of JSON, repositories, or external systems.

```csharp
public interface IDomainEvent { }

public sealed record TicketCreatedDomainEvent(
    TicketId TicketId, string Title, TicketPriority Priority) : IDomainEvent;

public sealed record StatusChangedDomainEvent(
    TicketId TicketId, TicketStatus FromStatus, TicketStatus ToStatus) : IDomainEvent;

public sealed record AssigneeChangedDomainEvent(
    TicketId TicketId, string? PreviousAssignee, string? NewAssignee) : IDomainEvent;

public sealed record CommentAddedDomainEvent(
    TicketId TicketId, string? Author, string Content) : IDomainEvent;
```

Handlers call `ticket.ClearDomainEvents()` after consuming all events, before
calling `SaveChangesAsync`.

### `AuditEvent` – Append-Only Audit Record

```csharp
public sealed class AuditEvent
{
    public AuditEventId Id { get; }
    public TicketId TicketId { get; }
    public string EventType { get; }   // See AuditEventTypes constants
    public string? Actor { get; }
    public DateTimeOffset OccurredAt { get; }
    public string Payload { get; }     // JSON string, serialised in Application layer
}

public static class AuditEventTypes
{
    public const string TicketCreated   = "ticket.created";
    public const string StatusChanged   = "ticket.status_changed";
    public const string AssigneeChanged = "ticket.assignee_changed";
    public const string CommentAdded    = "ticket.comment_added";
}
```

`AuditEvent` is append-only: once written it is never modified. There is no FK
constraint to `Tickets` — audit records survive ticket deletion.
See ADR 0020 for the payload format decision.

### `OutboxMessage` – Reliable Publication Stub

```csharp
public sealed class OutboxMessage
{
    public OutboxMessageId Id { get; }
    public string EventType { get; }     // Same convention as AuditEventTypes
    public string Payload { get; }       // JSON string
    public DateTimeOffset OccurredAt { get; }
    public DateTimeOffset? DispatchedAt { get; private set; }
    public OutboxMessageStatus Status { get; } // Computed: Pending | Dispatched

    public void MarkDispatched(DateTimeOffset dispatchedAt)
}

public enum OutboxMessageStatus { Pending, Dispatched }
```

**SHOWCASE STUB** — demonstrates the Outbox pattern at the architectural
level. A production implementation would add a background dispatcher, retry
counters, and dead-letter handling. See ADR 0021.

### `Guard` – Invariant Enforcement

```csharp
public static class Guard
{
    // Throws DomainException(code: "domain.not_empty")
    public static void NotNullOrWhiteSpace(string? value, string paramName)

    // Throws DomainException(code: "domain.max_length")
    public static void MaxLength(string value, int maxLength, string paramName)

    // Throws DomainException(code: "guard.not_null")
    public static void NotNull<T>(T? value, string paramName) where T : class
}
```

### Exception Types

```csharp
public sealed record DomainError(string Code, string Message);

public sealed class DomainException(DomainError error) : Exception(error.Message)
{
    public DomainError Error { get; } = error;
}
```

Domain exceptions are caught in Application handlers and mapped to
`Result.DomainViolation(...)`. They must never reach the HTTP layer as raw
exceptions.
