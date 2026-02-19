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

    public async Task<Result<SearchTickesResult>> HandleAsync(
        SearchTicketsQuery? query,
        CancellationToken ct = default)
    {
        if (query is null)
            return Result<SearchTickesResult>.Validation(
                "search_tickets.query.null",
                "Query must not be null.");

        // Defensive defaults
        var criteria = query.Criteria ?? new TicketSearchCriteria();
        var paging = query.Paging;
        var sort =  query.Sort ?? SortSpec.Default;
        
        // Transport-Validation
        if (paging.Page <= 0)
            return Result<SearchTickesResult>.Validation(
                "search_tickets.paging.page.invalid",
                "Page must be 1 or greater.");
        
        if (paging.PageSize is <= 0 or > 200)
            return Result<SearchTickesResult>.Validation(
                "search_tickets.paging.pageSize.invalid",
                "PageSize must be between 1 and 200.");

        var page = await _repository.SearchAsync(criteria, paging, sort, ct);

        var dtoPage = new PagedResult<TicketListItemDto>(
            Items: page.Items.Select(ToDto).ToList(),
            TotalCount: page.TotalCount,
            Paging: page.Paging);

        return Result<SearchTickesResult>.Success(new SearchTickesResult(dtoPage));
    }

    private static TicketListItemDto ToDto(Ticket ticket) => new(
        Id: ticket.Id,
        Title: ticket.Title,
        Status: ticket.Status,
        Priority: ticket.Priority,
        CreatedAt: ticket.CreatedAt,
        DueAt: ticket.DueAt);
}
