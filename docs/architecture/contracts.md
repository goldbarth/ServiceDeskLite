## Contracts Layer (`ServiceDeskLite.Contracts`)

All public HTTP types are versioned under `V1/`. Enum properties in responses are strings (not numeric), enforced by `JsonStringEnumConverter` in the API.

### Request / Response DTOs

```csharp
// POST /api/v1/tickets
public sealed record CreateTicketRequest(
    string Title,
    string Description,
    TicketPriority Priority,
    DateTimeOffset? DueAt);

public sealed record CreateTicketResponse(Guid Id);

// POST /api/v1/tickets/{id}/status
public sealed record ChangeTicketStatusRequest(TicketStatus NewStatus);

// POST /api/v1/tickets/{id}/assign
public sealed record AssignTicketRequest(string? AssigneeName);

// POST /api/v1/tickets/{id}/comments
public sealed record AddCommentRequest(string Content, string? Author);

public sealed record CommentResponse(Guid Id, string Content, string? Author, DateTimeOffset CreatedAt);

// GET /api/v1/tickets/{id}
public sealed record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    string? Assignee,
    IReadOnlyList<ConversationItemResponse> Conversation,   // merged comments + events
    IReadOnlyList<TicketStatus> AllowedTransitions,         // server-side workflow rules
    bool IsOverdue,                                          // server-side computed
    string DisplayRef,                                       // e.g. "TKT-0042"
    string StatusGuidance,
    IReadOnlyList<string> SuggestedNextSteps);

// GET /api/v1/tickets
public sealed record SearchTicketsRequest(
    int Page = 1,
    int PageSize = 25,
    TicketSortField? SortField = null,
    SortDirection? SortDirection = null);

public sealed record TicketListItemResponse(
    Guid Id,
    string Title,
    TicketPriority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    string DisplayRef,
    bool IsOverdue);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

// GET /api/v1/tickets/{id}/audit-events
public sealed record AuditEventResponse(
    Guid Id,
    string EventType,
    string? Actor,
    DateTimeOffset OccurredAt,
    AuditEventPayload Payload);

// GET /api/v1/dashboard/summary
public sealed record DashboardSummaryResponse(
    int NewCount,
    int TriagedCount,
    int InProgressCount,
    int OverdueCount,
    int ResolvedLast7DaysCount);
```

### Conversation Item

A `ConversationItemResponse` merges comments and system audit events into a unified timeline, sorted by timestamp.

```csharp
public enum ConversationItemKind { Comment, SystemEvent }

public sealed record ConversationItemResponse(
    DateTimeOffset Timestamp,
    ConversationItemKind Kind,
    CommentResponse? Comment,
    AuditEventResponse? Event);
```

### Polymorphic Audit Payloads

`AuditEventPayload` uses `[JsonPolymorphic]` with a `$type` discriminator field.

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TicketCreatedPayload),         "ticket.created")]
[JsonDerivedType(typeof(TicketStatusChangedPayload),   "ticket.status_changed")]
[JsonDerivedType(typeof(TicketAssigneeChangedPayload), "ticket.assignee_changed")]
[JsonDerivedType(typeof(TicketCommentAddedPayload),    "ticket.comment_added")]
[JsonDerivedType(typeof(RawAuditEventPayload),         "unknown")]
public abstract record AuditEventPayload;

public sealed record TicketCreatedPayload(string Title, string Priority) : AuditEventPayload;

public sealed record TicketStatusChangedPayload(
    string FromStatus, string ToStatus) : AuditEventPayload;

public sealed record TicketAssigneeChangedPayload(
    string? PreviousAssignee, string? NewAssignee) : AuditEventPayload;

public sealed record TicketCommentAddedPayload(
    string Author, string Content) : AuditEventPayload;

// Fallback for unknown event types — preserves raw JSON without crashing.
public sealed record RawAuditEventPayload(string RawJson) : AuditEventPayload;
```

See ADR 0020 for the payload format decision.

### Contracts Enums

```csharp
public enum TicketPriority  { Low, Medium, High, Critical }
public enum TicketStatus    { New, Triaged, InProgress, Waiting, Resolved, Closed }
public enum TicketSortField { CreatedAt, DueAt, Priority, Status, Title }
public enum SortDirection   { Asc, Desc }
```

### ProblemDetails Extension Keys

```csharp
public static class ContractsProblemDetailsConventions
{
    public static class ExtensionKeys
    {
        public const string Code      = "code";
        public const string ErrorType = "errorType";
        public const string Meta      = "meta";
        public const string TraceId   = "traceId";
    }
}
```