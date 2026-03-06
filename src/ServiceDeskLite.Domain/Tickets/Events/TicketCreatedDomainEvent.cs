using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets.Events;

public sealed record TicketCreatedDomainEvent(
    TicketId TicketId,
    string Title,
    TicketPriority Priority) : IDomainEvent;
