using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.SearchTickets;

public class SearchTicketsHandler
{
    private readonly ITicketRepository _repository;

    public SearchTicketsHandler(ITicketRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Result<SearchTicketsResult>> HandleAsync(
        SearchTicketsQuery? query,
        CancellationToken ct = default)
    {
        if (query is null)
            return Result<SearchTicketsResult>.Validation(
                "search_tickets.query.null",
                "Query must not be null.");

        var criteria = query.Criteria ?? new TicketSearchCriteria();
        var paging = query.Paging;
        var sort = query.Sort ?? SortSpec.Default;

        var page = await _repository.SearchAsync(criteria, paging, sort, ct);

        var dtoPage = new PagedResult<TicketListItemDto>(
            Items: page.Items.Select(ToDto).ToList(),
            TotalCount: page.TotalCount,
            Paging: page.Paging);

        return Result<SearchTicketsResult>.Success(new SearchTicketsResult(dtoPage));
    }

    private static TicketListItemDto ToDto(Ticket ticket) => new(
        Id: ticket.Id,
        Title: ticket.Title,
        Status: ticket.Status,
        Priority: ticket.Priority,
        CreatedAt: ticket.CreatedAt,
        DueAt: ticket.DueAt);
}
