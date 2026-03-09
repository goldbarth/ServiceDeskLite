using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets.Events;

public sealed record CommentAddedDomainEvent(
    TicketId TicketId,
    string? Author,
    string Content) : IDomainEvent;
