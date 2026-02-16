using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Application.Tickets.SearchTickets;

public class SearchTicketsHandlerTests
{
    [Fact]
    public async Task Defaults_are_applied_when_paging_and_sort_are_null()
    {
        var repo = new FakeTicketRepository();
        var handler = new SearchTicketsHandler(repo);

        var query = new SearchTicketsQuery(Criteria: new TicketSearchCriteria(Text: "abc"));
        var result = await handler.HandleAsync(query);
        
        result.IsSuccess.Should().BeTrue();
        repo.LastPaging.Should().Be(Paging.Default);
        repo.LastSort.Should().Be(SortSpec.Default);
    }

    [Fact]
    public async Task Returns_validation_error_for_invalid_page()
    {
        var repo = new FakeTicketRepository();
        var handler = new SearchTicketsHandler(repo);
        
        var query = new SearchTicketsQuery(
            Criteria: new TicketSearchCriteria(),
            Paging: new Paging(Page: 0, PageSize: 25),
            Sort: SortSpec.Default);
        
        var result = await handler.HandleAsync(query);
        
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("search_tickets.paging.page.invalid");
    }
    
    private sealed class FakeTicketRepository :  ITicketRepository
    {
        public Paging? LastPaging { get; private set; }
        public SortSpec? LastSort { get; private set; }
        
        
        public Task AddAsync(Ticket ticket, CancellationToken ct = default)
            => Task.CompletedTask;
        
        public Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default)
            => Task.FromResult<Ticket?>(null);

        public Task<bool> ExistsAsync(TicketId id, CancellationToken ct)
            => Task.FromResult(false);

        public Task<PagedResult<Ticket>> SearchAsync(
            TicketSearchCriteria criteria, 
            Paging paging, 
            SortSpec sort, 
            CancellationToken ct = default)
        {
            LastPaging = paging;
            LastSort = sort;

            return Task.FromResult(new PagedResult<Ticket>(
                Items: [],
                TotalCount: 0,
                Paging: paging));
        }
    }
}
