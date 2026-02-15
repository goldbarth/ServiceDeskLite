using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Application.Tickets.GetTicketById;

public class GetTicketByIdHandlerTests
{
    [Fact]
    public async Task Returns_not_found_when_ticket_missing()
    {
        var repo =  new FakeTicketRepository(null);
        var handler = new GetTicketByIdHandler(repo);
        
        var result = await handler.HandleAsync(new GetTicketByIdQuery(TicketId.New()));
        
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("ticket.not_found");
    }

    [Fact]
    public async Task Returns_ticket_details_when_ticket_exists()
    {
        var id = TicketId.New();
        var ticket = new Ticket(
            id,
            "Title",
            "Desc",
            TicketPriority.Medium,
            DateTimeOffset.UtcNow);
        
        var repo =  new FakeTicketRepository(ticket);
        var handler = new GetTicketByIdHandler(repo);
        
        var result = await handler.HandleAsync(new GetTicketByIdQuery(id));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(id);
        result.Value.Title.Should().Be("Title");
    }
    
    private sealed class FakeTicketRepository : ITicketRepository
    {
        private readonly Ticket? _ticket;
        
        public FakeTicketRepository(Ticket? ticket) => _ticket = ticket;
        
        public Task AddAsync(Ticket ticket, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default)
            => Task.FromResult(_ticket is not null && _ticket.Id.Equals(id) ? _ticket : null);

        public Task<PagedResult<TicketListItemDto>> SearchAsync(
            TicketSearchCriteria criteria,
            Paging paging,
            SortSpec sort,
            CancellationToken ct = default)
            => Task.FromResult(new PagedResult<TicketListItemDto>(
                Items: [],
                TotalCount: 0,
                Paging: paging));
    }
}
