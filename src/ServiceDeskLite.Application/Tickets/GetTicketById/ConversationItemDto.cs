using ServiceDeskLite.Application.Tickets.GetAuditEvents;
using ServiceDeskLite.Application.Tickets.Shared;

namespace ServiceDeskLite.Application.Tickets.GetTicketById;

public enum ConversationItemKind
{
    Comment,
    SystemEvent
}

public sealed record ConversationItemDto(
    DateTimeOffset Timestamp,
    ConversationItemKind Kind,
    CommentDto? Comment,
    AuditEventDto? Event);