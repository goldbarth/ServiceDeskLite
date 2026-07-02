namespace ServiceDeskLite.Domain.Audit;

/// <summary>
/// String constants for audit event types.
/// Using string constants instead of an enum allows new event types to be added
/// without requiring a database migration (the column is TEXT).
/// Convention: dot-separated, lowercase, aggregate-prefixed.
/// </summary>
public static class AuditEventTypes
{
    public const string TicketCreated    = "ticket.created";
    public const string StatusChanged    = "ticket.status_changed";
    public const string AssigneeChanged  = "ticket.assignee_changed";
    public const string CommentAdded     = "ticket.comment_added";
    public const string DetailsUpdated   = "ticket.details_updated";
}
