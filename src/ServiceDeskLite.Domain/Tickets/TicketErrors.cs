using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets;

public static class TicketErrors
{
    public static DomainError InvalidTransition(TicketStatus from, TicketStatus to) =>
        new(
            "domain.ticket.status.invalid_transition",
            $"Invalid status transition from {from} to {to}."
        );
}
