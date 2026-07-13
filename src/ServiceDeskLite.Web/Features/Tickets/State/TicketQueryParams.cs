using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;

namespace ServiceDeskLite.Web.Features.Tickets.State;

/// <summary>
/// Immutable value object representing the query state of the tickets list.
/// Use <c>with</c> expressions to derive new instances for state transitions.
/// </summary>
public sealed record TicketQueryParams(
    int Page,
    int PageSize,
    TicketSortField SortField,
    SortDirection SortDirection,
    string? Q = null,
    TicketStatus[]? Statuses = null,
    TicketPriority[]? Priorities = null,
    string? Assignee = null,
    bool Unassigned = false,
    bool Overdue = false)
{
    public static readonly TicketQueryParams Default = new(
        Page: 1,
        PageSize: 25,
        SortField: TicketSortField.CreatedAt,
        SortDirection: SortDirection.Desc);

    public TicketQueryParams WithSort(TicketSortField field)
    {
        if (SortField == field)
            return this with { SortDirection = SortDirection == SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc, Page = 1 };

        return this with { SortField = field, SortDirection = SortDirection.Asc, Page = 1 };
    }

    public bool HasActiveFilters() =>
        !string.IsNullOrWhiteSpace(Q) ||
        Statuses is { Length: > 0 } ||
        Priorities is { Length: > 0 } ||
        !string.IsNullOrWhiteSpace(Assignee) ||
        Unassigned ||
        Overdue;

    public SearchTicketsRequest ToSearchRequest() =>
        new(Page: Page, PageSize: PageSize, SortField: SortField, SortDirection: SortDirection,
            Q: Q, Statuses: Statuses, Priorities: Priorities, Assignee: Assignee,
            Unassigned: Unassigned ? true : null, Overdue: Overdue ? true : null);
}
