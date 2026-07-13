using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.Shared;

public sealed record TicketSearchCriteria(
    string? Text = null,
    IReadOnlyCollection<TicketStatus>? Statuses = null,
    IReadOnlyCollection<TicketPriority>? Priorities = null,
    string? AssigneeName = null,
    // Display-ref suffix (the 6 hex chars behind '#'); matches a ticket by its reference number.
    string? Reference = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    DateTimeOffset? DueFrom = null,
    DateTimeOffset? DueTo = null,
    // Only tickets without an assigned agent; ANDs with AssigneeName (both set matches nothing).
    bool Unassigned = false,
    // Only open tickets past their due date, judged against the repository's clock so the
    // filter agrees with the IsOverdue flag it projects.
    bool Overdue = false)
{
    public static readonly TicketSearchCriteria Empty = new();
}
