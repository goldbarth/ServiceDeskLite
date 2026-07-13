using ServiceDeskLite.Contracts.V1.Common;

namespace ServiceDeskLite.Contracts.V1.Tickets;

/// <param name="Assignee">Free-text match on the assigned agent's name. Cannot express "nobody"; use <paramref name="Unassigned"/> for that.</param>
/// <param name="Unassigned">When true, only tickets without an assigned agent. Combined with <paramref name="Assignee"/> the filters AND together and match nothing.</param>
/// <param name="Overdue">When true, only tickets whose due date lies in the past and that are still open (not Resolved/Closed). "Past" is evaluated server-side per request, so a bookmarked URL stays correct.</param>
public sealed record SearchTicketsRequest(
    int Page = 1,
    int PageSize = 25,
    TicketSortField? SortField = null,
    SortDirection? SortDirection = null,
    string? Q = null,
    TicketStatus[]? Statuses = null,
    TicketPriority[]? Priorities = null,
    string? Assignee = null,
    bool? Unassigned = null,
    bool? Overdue = null
);
