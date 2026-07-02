using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets;

public static class TicketErrors
{
    public const string InvalidTransitionCode = "domain.ticket.status.invalid_transition";
    public const string CannotAssignClosedCode = "domain.ticket.assign.closed";
    public const string CannotUpdateClosedCode = "domain.ticket.update.closed";

    public static DomainError InvalidTransition(TicketStatus from, TicketStatus to) =>
        new(
            InvalidTransitionCode,
            $"Invalid status transition from {from} to {to}."
        );

    public static DomainError CannotAssignClosed() =>
        new(CannotAssignClosedCode, "Cannot assign a closed ticket.");

    public static DomainError CannotUpdateClosed() =>
        new(CannotUpdateClosedCode, "Cannot update a closed ticket.");
}
