using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Tickets;

public sealed class ReadIsolationTests
{
    [Theory]
    [ProviderMatrix]
    public async Task Search_does_not_return_staged_but_uncommitted_tickets(PersistenceProvider provider)
    {
        using var host = TestServiceProvider.Create(provider);

        // Scope 1: Stage a ticket without committing
        using (var scope = host.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
            var ticket = new Ticket(
                TicketId.New(), "Staged only", "Not committed",
                TicketPriority.Medium, DateTimeOffset.UtcNow);

            await repo.AddAsync(ticket);

            // Read within the SAME scope must NOT see uncommitted data
            // (important for InMemory staging isolation)
            var searchHandler = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var result = await searchHandler.HandleAsync(
                new SearchTicketsQuery(new TicketSearchCriteria(), new Paging()));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Page.TotalCount.Should().Be(0,
                "staged but uncommitted tickets must not be visible in reads");
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Search_returns_committed_tickets_from_prior_scope(PersistenceProvider provider)
    {
        using var host = TestServiceProvider.Create(provider);

        // Scope 1: Create and commit
        using (var scope = host.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var ticket = new Ticket(
                TicketId.New(), "Committed", "Visible",
                TicketPriority.Medium, DateTimeOffset.UtcNow);

            await repo.AddAsync(ticket);
            await uow.SaveChangesAsync();
        }

        // Scope 2: Search must see the committed ticket
        using (var scope = host.CreateScope())
        {
            var searchHandler = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var result = await searchHandler.HandleAsync(
                new SearchTicketsQuery(new TicketSearchCriteria(),  new Paging()));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Page.TotalCount.Should().Be(1);
        }
    }
}
