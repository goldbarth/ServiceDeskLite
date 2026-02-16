using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.Shared;

public sealed record TicketSearchCriteria(
    string? Text = null,
    IReadOnlyCollection<TicketStatus>? Statuses = null,
    IReadOnlyCollection<TicketPriority>? Priorities = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    DateTimeOffset? DueFrom = null,
    DateTimeOffset? DueTo = null);
