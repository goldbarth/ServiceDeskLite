using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Tickets;

public sealed class DuplicateDetectionTests
{
    [Fact]
    public async Task InMemory_staged_duplicate_id_causes_commit_failure()
    {
        using var host = TestServiceProvider.Create(PersistenceProvider.InMemory);

        var id = TicketId.New();

        using var scope = host.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var ticket1 = new Ticket(id, "First", "First ticket",
            TicketPriority.Medium, DateTimeOffset.UtcNow);
        var ticket2 = new Ticket(id, "Duplicate", "Duplicate ticket",
            TicketPriority.Medium, DateTimeOffset.UtcNow);

        await repo.AddAsync(ticket1);
        await repo.AddAsync(ticket2);

        var act = () => uow.SaveChangesAsync();
        await act.Should().ThrowAsync<Exception>("committing duplicate IDs must fail");
    }

    [Fact]
    public async Task InMemory_committed_state_remains_consistent_after_duplicate_failure()
    {
        using var host = TestServiceProvider.Create(PersistenceProvider.InMemory);

        var sharedId = TicketId.New();

        // Scope 1: Commit the first ticket
        using (var scope = host.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var ticket = new Ticket(sharedId, "Original", "First version",
                TicketPriority.Low, DateTimeOffset.UtcNow);

            await repo.AddAsync(ticket);
            await uow.SaveChangesAsync();
        }

        // Scope 2: Try to commit a duplicate — must fail
        using (var scope = host.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var duplicate = new Ticket(sharedId, "Duplicate", "Should not overwrite",
                TicketPriority.High, DateTimeOffset.UtcNow);

            await repo.AddAsync(duplicate);

            var act = () => uow.SaveChangesAsync();
            await act.Should().ThrowAsync<Exception>();
        }

        // Scope 3: Committed state must still have the original ticket
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<GetTicketByIdHandler>();
            var result = await handler.HandleAsync(new GetTicketByIdQuery(sharedId));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Title.Should().Be("Original",
                "original committed ticket must survive a failed duplicate commit");
        }
    }

    [Fact]
    public async Task InMemory_search_count_unaffected_by_failed_duplicate_commit()
    {
        using var host = TestServiceProvider.Create(PersistenceProvider.InMemory);

        var id = TicketId.New();

        // Commit one ticket
        using (var scope = host.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await repo.AddAsync(new Ticket(id, "Only one", "Should stay one",
                TicketPriority.Medium, DateTimeOffset.UtcNow));
            await uow.SaveChangesAsync();
        }

        // Attempt duplicate commit — expect failure
        using (var scope = host.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await repo.AddAsync(new Ticket(id, "Dup", "Dup desc",
                TicketPriority.Medium, DateTimeOffset.UtcNow));

            try { await uow.SaveChangesAsync(); } catch { /* expected */ }
        }

        // Total count must still be 1
        using (var scope = host.CreateScope())
        {
            var searchHandler = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var result = await searchHandler.HandleAsync(
                new SearchTicketsQuery(new TicketSearchCriteria(), Paging.Default));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Page.TotalCount.Should().Be(1,
                "failed duplicate commit must not increase total count");
        }
    }
}
