namespace ServiceDeskLite.Contracts.V1.Tickets;

public enum ConversationItemKind
{
    Comment,
    SystemEvent
}

public sealed record ConversationItemResponse(
    DateTimeOffset Timestamp,
    ConversationItemKind Kind,
    CommentResponse? Comment,
    AuditEventResponse? Event);