using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.Shared;

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

        return Result<SearchTicketsResult>.Success(new SearchTicketsResult(page));
    }
}
