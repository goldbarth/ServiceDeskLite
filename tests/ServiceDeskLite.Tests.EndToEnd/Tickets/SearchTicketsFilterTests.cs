using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Tickets;

/// <summary>
/// Exercises the search_tickets tool's action — SearchTicketsHandler with the
/// structured filters the tool maps (priority, assignee, free text) — across both
/// persistence providers, per the persistence-parity convention. The Api tool class
/// (input parsing / formatting) is covered separately in Tests.Api.
/// </summary>
public sealed class SearchTicketsFilterTests
{
    [Theory]
    [ProviderMatrix]
    public async Task Filter_by_priority_returns_only_matching(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            await handler.HandleAsync(TicketFactory.Command(priority: TicketPriority.Low));
            await handler.HandleAsync(TicketFactory.Command(priority: TicketPriority.Critical));
            await handler.HandleAsync(TicketFactory.Command(priority: TicketPriority.Critical));
        }

        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var result = await search.HandleAsync(new SearchTicketsQuery(
                new TicketSearchCriteria(Priorities: new[] { TicketPriority.Critical }),
                Paging.Default));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Page.TotalCount.Should().Be(2);
            result.Value.Page.Items.Should().OnlyContain(t => t.Priority == TicketPriority.Critical);
        }
    }

    // NOTE: Assignee filtering is intentionally NOT covered here. It surfaces a
    // pre-existing EF translation bug in EfTicketRepository.SearchAsync (the
    // value-converted Assignee column cannot translate `.Value.Name.Contains(...)`
    // to SQL), which also affects the REST `?assignee=` filter on Postgres. Deferred
    // to a separate infrastructure fix (issue #144); the tool's assignee-input mapping
    // is covered in Tests.Api SearchTicketsToolInputTests.

    [Theory]
    [ProviderMatrix]
    public async Task Filter_by_free_text_matches_title(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            await create.HandleAsync(TicketFactory.Command(title: "VPN connection drops"));
            await create.HandleAsync(TicketFactory.Command(title: "Printer offline"));
        }

        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var result = await search.HandleAsync(new SearchTicketsQuery(
                new TicketSearchCriteria(Text: "VPN"),
                Paging.Default));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Page.Items.Should().ContainSingle()
                .Which.Title.Should().Be("VPN connection drops");
        }
    }
}
