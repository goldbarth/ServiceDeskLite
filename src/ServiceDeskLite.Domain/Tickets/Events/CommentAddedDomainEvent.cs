using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets.Events;

public sealed record CommentAddedDomainEvent(
    TicketId TicketId,
    CommentId CommentId,
    string? Author,
    int ContentLength) : IDomainEvent;
