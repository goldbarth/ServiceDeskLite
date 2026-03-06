using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets.Events;

// PreviousAssignee and NewAssignee are nullable: null means unassigned.
public sealed record AssigneeChangedDomainEvent(
    TicketId TicketId,
    string? PreviousAssignee,
    string? NewAssignee) : IDomainEvent;
