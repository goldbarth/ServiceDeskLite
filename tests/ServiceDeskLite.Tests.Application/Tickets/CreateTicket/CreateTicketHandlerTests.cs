using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Application.Tickets.CreateTicket;

public class CreateTicketHandlerTests
{
    [Fact]
    public async Task Returns_validation_error_when_title_missing()
    {
        var repo = new FakeTicketRepository();
        var uow = new FakeUnitOfWork();
        var handler = new CreateTicketHandler(repo, uow);

        var cmd = new CreateTicketCommand(
            Title: " ",
            Description: "desc",
            Priority: TicketPriority.Medium,
            CreatedAt: DateTimeOffset.UtcNow);
        
        var result = await handler.HandleAsync(cmd);
        
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("create_ticket.title.required");
        uow.SaveCalls.Should().Be(0);
        repo.AddCalls.Should().Be(0);
    }
    
    private sealed class FakeTicketRepository : ITicketRepository
    {
        public int AddCalls { get; private set; }

        public Task AddAsync(Ticket ticket, CancellationToken ct = default)
        {
            AddCalls++;
            return Task.CompletedTask;
        }

        public Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default)
            => Task.FromResult<Ticket?>(null);

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
    
    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        
        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }
}
