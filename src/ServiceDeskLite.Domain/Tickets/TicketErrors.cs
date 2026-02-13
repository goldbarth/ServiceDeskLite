using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets;

public static class TicketErrors
{
    public static DomainError InvalidTransition(TicketStatus from, TicketStatus to) =>
        new(
            "ticket.invalid_transition",
            $"Invalid status transition from {from} to {to}."
        );
}
