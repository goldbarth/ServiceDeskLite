using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Tickets;

/// <summary>
/// Hybrid ticket retrieval parity (issue #157). The test host configures no Voyage
/// key, so the semantic signal is unavailable on both providers; the blend must then
/// degrade to keyword-only — reporting SemanticAvailable=false while still returning
/// keyword hits — and metadata filters must constrain results identically on Postgres
/// and InMemory.
/// </summary>
public sealed class HybridTicketSearchTests
{
    [Theory]
    [ProviderMatrix]
    public async Task Degrades_to_keyword_and_finds_ticket_on_both_providers(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            var command = TicketFactory.Command(priority: TicketPriority.High, title: "Kerberos SSO outage");
            (await create.HandleAsync(command)).IsSuccess.Should().BeTrue();
        }

        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<IHybridTicketSearch>();

            var result = await search.SearchAsync(
                new HybridTicketSearchQuery("Kerberos", 5), CancellationToken.None);

            result.SemanticAvailable.Should().BeFalse("no Voyage key is configured in the test host");
            result.Matches.Should().Contain(m => m.Title == "Kerberos SSO outage" && m.FromKeyword && !m.FromSemantic);
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Priority_filter_constrains_results_on_both_providers(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            (await create.HandleAsync(TicketFactory.Command(priority: TicketPriority.Low, title: "Printer jam kiosk")))
                .IsSuccess.Should().BeTrue();
        }

        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<IHybridTicketSearch>();

            var high = await search.SearchAsync(
                new HybridTicketSearchQuery("Printer", 5, Priorities: [TicketPriority.High]), CancellationToken.None);
            high.Matches.Should().NotContain(m => m.Title == "Printer jam kiosk");

            var low = await search.SearchAsync(
                new HybridTicketSearchQuery("Printer", 5, Priorities: [TicketPriority.Low]), CancellationToken.None);
            low.Matches.Should().Contain(m => m.Title == "Printer jam kiosk");
        }
    }
}
