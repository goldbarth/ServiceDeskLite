using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets.Events;

public sealed record StatusChangedDomainEvent(
    TicketId TicketId,
    TicketStatus FromStatus,
    TicketStatus ToStatus) : IDomainEvent;
