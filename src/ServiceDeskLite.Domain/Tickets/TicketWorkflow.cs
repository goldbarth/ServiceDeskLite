using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets
{
    public static class TicketWorkflow
    {
        private static readonly HashSet<(TicketStatus From, TicketStatus To)> _allowed =
        [
            (TicketStatus.New, TicketStatus.Triaged),

            (TicketStatus.Triaged, TicketStatus.InProgress),
            (TicketStatus.Triaged, TicketStatus.Waiting),
            (TicketStatus.Triaged, TicketStatus.Resolved),

            (TicketStatus.InProgress, TicketStatus.Waiting),
            (TicketStatus.InProgress, TicketStatus.Resolved),

            (TicketStatus.Waiting, TicketStatus.InProgress),
            (TicketStatus.Waiting, TicketStatus.Resolved),

            (TicketStatus.Resolved, TicketStatus.Closed),
            (TicketStatus.Resolved, TicketStatus.InProgress)
        ];

        public static bool CanTransition(TicketStatus from, TicketStatus to)
            => _allowed.Contains((from, to));

        public static void EnsureCanTransition(TicketStatus from, TicketStatus to)
        {
            if (!CanTransition(from, to))
                throw new DomainException(TicketErrors.InvalidTransition(from, to));
        }
    }
}

