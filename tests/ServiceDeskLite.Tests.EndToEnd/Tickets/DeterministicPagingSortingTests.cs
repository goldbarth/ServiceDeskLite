using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Tickets;

public sealed class DeterministicPagingSortingTests
{
    [Theory]
    [ProviderMatrix]
    public async Task Default_sort_returns_newest_first_with_id_tiebreaker(PersistenceProvider provider)
    {
        using var host = TestServiceProvider.Create(provider);
        var sameTime = DateTimeOffset.UtcNow;

        // Create 3 tickets with identical CreatedAt — ordering must be deterministic via Id tiebreaker
        var ids = new List<TicketId>();
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            for (var i = 0; i < 3; i++)
            {
                var result = await handler.HandleAsync(TicketFactory.Command(createdAt: sameTime));
                result.IsSuccess.Should().BeTrue();
                ids.Add(result.Value!.Id);
            }
        }

        // Search with default sort (CreatedAt Desc, Id Asc tiebreaker)
        using (var scope = host.CreateScope())
        {
            var searchHandler = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var result = await searchHandler.HandleAsync(
                new SearchTicketsQuery(new TicketSearchCriteria()));

            result.IsSuccess.Should().BeTrue();
            var items = result.Value!.Page.Items;
            items.Should().HaveCount(3);

            // Same CreatedAt → tiebreaker is Id ascending
            var returnedIds = items.Select(t => t.Id).ToList();
            returnedIds.Should().BeInAscendingOrder(id => id.Value,
                "tickets with same CreatedAt should be sorted by Id ascending as tiebreaker");
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Paging_returns_stable_pages_across_scopes(PersistenceProvider provider)
    {
        using var host = TestServiceProvider.Create(provider);
        var baseTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Create 5 tickets with distinct CreatedAt
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            for (var i = 0; i < 5; i++)
            {
                var result = await handler.HandleAsync(
                    TicketFactory.Command(createdAt: baseTime.AddMinutes(i)));
                result.IsSuccess.Should().BeTrue();
            }
        }

        // Page 1 (size 2) + Page 2 (size 2) + Page 3 (size 2) must cover all 5, no overlap
        var allPagedIds = new List<TicketId>();

        using (var scope = host.CreateScope())
        {
            var searchHandler = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();

            for (var page = 1; page <= 3; page++)
            {
                var result = await searchHandler.HandleAsync(
                    new SearchTicketsQuery(
                        new TicketSearchCriteria(),
                        new Paging(page, 2),
                        SortSpec.Default));

                result.IsSuccess.Should().BeTrue();
                result.Value!.Page.TotalCount.Should().Be(5);
                allPagedIds.AddRange(result.Value.Page.Items.Select(t => t.Id));
            }
        }

        allPagedIds.Should().HaveCount(5, "pages must cover all tickets without gaps");
        allPagedIds.Should().OnlyHaveUniqueItems("pages must not overlap");
    }

    [Theory]
    [ProviderMatrix]
    public async Task Sort_by_priority_asc_returns_low_before_high(PersistenceProvider provider)
    {
        using var host = TestServiceProvider.Create(provider);

        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();

            await handler.HandleAsync(TicketFactory.Command(priority: TicketPriority.Critical));
            await handler.HandleAsync(TicketFactory.Command(priority: TicketPriority.Low));
            await handler.HandleAsync(TicketFactory.Command(priority: TicketPriority.High));
        }

        using (var scope = host.CreateScope())
        {
            var searchHandler = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var result = await searchHandler.HandleAsync(
                new SearchTicketsQuery(
                    new TicketSearchCriteria(),
                    Paging.Default,
                    new SortSpec(TicketSortField.Priority, SortDirection.Asc)));

            result.IsSuccess.Should().BeTrue();
            var priorities = result.Value!.Page.Items.Select(t => t.Priority).ToList();
            priorities.Should().BeInAscendingOrder();
        }
    }
}
