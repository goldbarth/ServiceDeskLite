using ServiceDeskLite.Contracts.V1.Tickets;

namespace ServiceDeskLite.Web.Features.Tickets.State;

/// <summary>
/// A named filter combination over the ticket list. Applying one rewrites the filter
/// fields (and resets the page) but keeps sort and page size; whether a preset is
/// active is derived from the current query, so an edited filter deselects it instead
/// of the control lying about what the list shows.
/// </summary>
public sealed record TicketViewPreset(
    string Label,
    TicketStatus[]? Statuses = null,
    bool Unassigned = false,
    bool Overdue = false)
{
    public TicketQueryParams Apply(TicketQueryParams current) => current with
    {
        Page = 1,
        Q = null,
        Assignee = null,
        Priorities = null,
        Statuses = Statuses,
        Unassigned = Unassigned,
        Overdue = Overdue,
    };

    public bool Matches(TicketQueryParams query) =>
        string.IsNullOrWhiteSpace(query.Q)
        && string.IsNullOrWhiteSpace(query.Assignee)
        && query.Priorities is not { Length: > 0 }
        && query.Unassigned == Unassigned
        && query.Overdue == Overdue
        && SameStatuses(query.Statuses);

    private bool SameStatuses(TicketStatus[]? other)
    {
        var mine = Statuses ?? [];
        var theirs = other ?? [];
        return mine.Length == theirs.Length && mine.ToHashSet().SetEquals(theirs);
    }
}

/// <summary>
/// The saved views over the queue. "My open" is deliberately absent: ICurrentUser is a
/// constant demo owner without an agent identity, so a "my" filter would have nothing
/// honest to filter on until real auth lands.
/// </summary>
public static class TicketViewPresets
{
    public static readonly TicketViewPreset Open = new(
        "Open",
        Statuses: [TicketStatus.New, TicketStatus.Triaged, TicketStatus.InProgress, TicketStatus.Waiting]);

    public static readonly TicketViewPreset Unassigned = new("Unassigned", Unassigned: true);

    public static readonly TicketViewPreset SlaCritical = new("SLA critical", Overdue: true);

    public static readonly TicketViewPreset All = new("All");

    public static readonly IReadOnlyList<TicketViewPreset> AllPresets = [Open, Unassigned, SlaCritical, All];
}
