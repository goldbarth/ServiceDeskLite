using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets
{
    public static class TicketWorkflow
    {
        private static readonly (TicketStatus From, TicketStatus To)[] _transitions =
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

        private static readonly HashSet<(TicketStatus From, TicketStatus To)> _allowed = [.. _transitions];

        public static bool CanTransition(TicketStatus from, TicketStatus to)
            => _allowed.Contains((from, to));

        public static IReadOnlyList<TicketStatus> GetAllowedTransitions(TicketStatus from)
            => [.. _transitions
                .Where(transition => transition.From == from)
                .Select(transition => transition.To)];

        public static void EnsureCanTransition(TicketStatus from, TicketStatus to)
        {
            if (!CanTransition(from, to))
                throw new DomainException(TicketErrors.InvalidTransition(from, to));
        }
    }
}

