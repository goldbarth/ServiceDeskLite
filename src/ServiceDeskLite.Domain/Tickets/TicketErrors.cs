using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets;

public static class TicketErrors
{
    public const string InvalidTransitionCode = "domain.ticket.status.invalid_transition";

    public static DomainError InvalidTransition(TicketStatus from, TicketStatus to) =>
        new(
            InvalidTransitionCode,
            $"Invalid status transition from {from} to {to}."
        );
}
