using System.Text.Json;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Application.DependencyInjection;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Infrastructure.InMemory.DependencyInjection;

namespace ServiceDeskLite.Tests.Api.Assistant;

/// <summary>
/// Self-correction fallback (issue #155): when semantic search is unavailable
/// (InMemory has no pgvector, so ITicketSimilaritySearch reports unavailable),
/// find_similar_tickets must fall back to keyword search and return the matching
/// ticket labelled as a keyword hit, rather than dead-ending with "unavailable".
/// Runs against real InMemory services so the fallback path is exercised end to end.
/// </summary>
public sealed class FindSimilarTicketsToolFallbackTests
{
    private static ServiceProvider BuildInMemoryProvider()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructureInMemory();
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task Falls_back_to_keyword_when_semantic_unavailable()
    {
        await using var provider = BuildInMemoryProvider();

        using (var scope = provider.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            var command = new CreateTicketCommand(
                Title: "Kerberos authentication outage",
                Description: "Users cannot log in via SSO.",
                Priority: TicketPriority.High,
                CreatedAt: DateTimeOffset.UtcNow);
            (await create.HandleAsync(command)).IsSuccess.Should().BeTrue();
        }

        using (var scope = provider.CreateScope())
        {
            var tool = new FindSimilarTicketsTool(
                scope.ServiceProvider.GetRequiredService<ITicketSimilaritySearch>(),
                scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>(),
                NullLogger<FindSimilarTicketsTool>.Instance);

            var input = JsonSerializer.Deserialize<JsonElement>("""{ "query": "Kerberos" }""");
            var (content, isError, _, confidence) = await tool.ExecuteAsync(input, CancellationToken.None);

            isError.Should().BeFalse();
            content.Should().Contain("Keyword-matched", "semantic search is unavailable, so it falls back");
            content.Should().Contain("Kerberos authentication outage");
            confidence.Should().BeNull("keyword hits carry no vector similarity score");
        }
    }
}
