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

    public Ticket(
        TicketId id,
        string title,
        string description,
        TicketPriority priority,
        DateTimeOffset createdAt,
        DateTimeOffset? dueAt = null)   // Guard validates all inputs

    public void ChangeStatus(TicketStatus newStatus)   // Delegates to TicketWorkflow
}
```
`Ticket` is constructed via its constructor – no factory method, no ORM-only parameterless constructor visible externally. Guard validation runs inside the constructor.

### `TicketId` – Strongly-Typed ID

```csharp
public readonly record struct TicketId(Guid Value)
{
    public static TicketId New() => new(Guid.NewGuid());
}
```

### Enums

```csharp
public enum TicketStatus  { New, Triaged, InProgress, Waiting, Resolved, Closed }
public enum TicketPriority { Low, Medium, High, Critical }
```

### Domain Model

```mermaid
classDiagram
    class Ticket {
        +TicketId Id
        +string Title
        +string Description
        +TicketPriority Priority
        +TicketStatus Status
        +DateTimeOffset CreatedAt
        +DateTimeOffset? DueAt
        +Ticket(id, title, description, priority, createdAt, dueAt?)
        +ChangeStatus(newStatus) void
    }
    class TicketId {
        <<record struct>>
        +Guid Value
        +New()$ TicketId
    }
    class TicketStatus {
        <<enumeration>>
        New
        Triaged
        InProgress
        Waiting
        Resolved
        Closed
    }
    class TicketPriority {
        <<enumeration>>
        Low
        Medium
        High
        Critical
    }
    class TicketWorkflow {
        <<static>>
        +CanTransition(from, to)$ bool
        +EnsureCanTransition(from, to)$ void
    }
    class Guard {
        <<static>>
        +NotNullOrWhiteSpace(value, paramName)$ void
        +MaxLength(value, maxLength, paramName)$ void
        +NotNull~T~(value, paramName)$ void
    }
    class DomainException {
        +DomainError Error
    }
    class DomainError {
        <<record>>
        +string Code
        +string Message
    }

    Ticket "1" --> "1" TicketId : identified by
    Ticket --> TicketStatus : has
    Ticket --> TicketPriority : has
    Ticket ..> TicketWorkflow : delegates ChangeStatus to
    Ticket ..> Guard : validated by
    DomainException --> DomainError : carries
```

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

```mermaid
stateDiagram-v2
    [*] --> New : created

    New --> Triaged : triage

    Triaged --> InProgress : start work
    Triaged --> Waiting : defer
    Triaged --> Resolved : quick-resolve

    InProgress --> Waiting : block
    InProgress --> Resolved : complete

    Waiting --> InProgress : unblock
    Waiting --> Resolved : resolve

    Resolved --> Closed : close
    Resolved --> InProgress : reopen

    Closed --> [*]
```

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

Domain exceptions are caught in Application handlers and mapped to `Result.DomainViolation(...)`. They must never reach the HTTP layer as raw exceptions.