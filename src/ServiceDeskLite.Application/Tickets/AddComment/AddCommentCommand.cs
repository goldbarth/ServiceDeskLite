using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.AddComment;

public sealed record AddCommentCommand(
    TicketId TicketId,
    string Content,
    string? Author,
    DateTimeOffset CreatedAt);
