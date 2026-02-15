using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.Shared;

public sealed record TicketSearchCriteria(
    string? Text = null,
    TicketStatus? Status = null,
    TicketPriority? Priority = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    DateTimeOffset? DueFrom = null,
    DateTimeOffset? DueTo = null);
