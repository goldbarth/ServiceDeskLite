using System.Text.Json.Serialization;

namespace ServiceDeskLite.Contracts.V1.Tickets;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TicketCreatedPayload),          "ticket.created")]
[JsonDerivedType(typeof(TicketStatusChangedPayload),    "ticket.status_changed")]
[JsonDerivedType(typeof(TicketAssigneeChangedPayload),  "ticket.assignee_changed")]
[JsonDerivedType(typeof(TicketCommentAddedPayload),     "ticket.comment_added")]
[JsonDerivedType(typeof(RawAuditEventPayload),          "unknown")]
public abstract record AuditEventPayload;

public sealed record TicketCreatedPayload(
    string Title,
    string Priority) : AuditEventPayload;

public sealed record TicketStatusChangedPayload(
    string FromStatus,
    string ToStatus) : AuditEventPayload;

public sealed record TicketAssigneeChangedPayload(
    string? PreviousAssignee,
    string? NewAssignee) : AuditEventPayload;

public sealed record TicketCommentAddedPayload(
    string Author,
    string Content) : AuditEventPayload;

// Fallback for unknown or unrecognized event types – preserves the raw JSON string
// so the UI can still display something meaningful without crashing.
public sealed record RawAuditEventPayload(string RawJson) : AuditEventPayload;