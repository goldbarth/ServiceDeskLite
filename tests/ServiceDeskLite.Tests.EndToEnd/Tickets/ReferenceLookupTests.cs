using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Tickets;

/// <summary>
/// Looking a ticket up by its display reference ("#ABC123") through the search criteria,
/// as the assistant's search_tickets tool does, across both persistence providers. This
/// exercises the id-suffix match that must translate to SQL on Postgres.
/// </summary>
public sealed class ReferenceLookupTests
{
    [Theory]
    [ProviderMatrix]
    public async Task Reference_resolves_the_single_matching_ticket(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        string targetRef;
        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            // A few tickets so the ref filter has to discriminate.
            await create.HandleAsync(TicketFactory.Command());
            await create.HandleAsync(TicketFactory.Command());
            var target = (await create.HandleAsync(TicketFactory.Command())).Value!;
            targetRef = TicketReference.Format(target.Id); // "#ABC123"
        }

        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();

            var result = await search.HandleAsync(new SearchTicketsQuery(
                new TicketSearchCriteria(Reference: targetRef),
                Paging.Default));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Page.Items.Should().ContainSingle()
                .Which.DisplayRef.Should().Be(targetRef);
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Reference_is_case_insensitive_and_tolerates_missing_hash(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        string suffixNoHash;
        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            var target = (await create.HandleAsync(TicketFactory.Command())).Value!;
            suffixNoHash = TicketReference.Format(target.Id).TrimStart('#').ToLowerInvariant();
        }

        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();

            var result = await search.HandleAsync(new SearchTicketsQuery(
                new TicketSearchCriteria(Reference: suffixNoHash),
                Paging.Default));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Page.Items.Should().ContainSingle();
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Unknown_reference_returns_no_tickets(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            await create.HandleAsync(TicketFactory.Command());
        }

        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();

            var result = await search.HandleAsync(new SearchTicketsQuery(
                new TicketSearchCriteria(Reference: "#zzzzzz"),
                Paging.Default));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Page.Items.Should().BeEmpty();
        }
    }
}
