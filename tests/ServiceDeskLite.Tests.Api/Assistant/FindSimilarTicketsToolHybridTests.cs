using System.Text.Json;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Application.DependencyInjection;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Infrastructure.InMemory.DependencyInjection;

namespace ServiceDeskLite.Tests.Api.Assistant;

/// <summary>
/// Hybrid retrieval (issue #157) end to end against real InMemory services. InMemory
/// has no pgvector, so the semantic signal reports unavailable and the blend must
/// degrade to keyword-only — returning the matching ticket, labelled as weaker
/// evidence with a null confidence, rather than dead-ending or faking a vector score.
/// </summary>
public sealed class FindSimilarTicketsToolHybridTests
{
    private static ServiceProvider BuildInMemoryProvider()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructureInMemory();
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task Degrades_to_keyword_only_when_semantic_unavailable()
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
                scope.ServiceProvider.GetRequiredService<IHybridTicketSearch>(),
                NullLogger<FindSimilarTicketsTool>.Instance);

            var input = JsonSerializer.Deserialize<JsonElement>("""{ "query": "Kerberos" }""");
            var result = await tool.ExecuteAsync(input, CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.Content.Should().Contain("keyword-only search", "semantic search is unavailable on InMemory");
            result.Content.Should().Contain("Kerberos authentication outage");
            result.Content.Should().Contain("matched=keyword");
            result.Confidence.Should().BeNull("keyword-only hits carry no vector relevance score");
        }
    }

    [Fact]
    public async Task Metadata_filter_constrains_keyword_results()
    {
        await using var provider = BuildInMemoryProvider();

        using (var scope = provider.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            (await create.HandleAsync(new CreateTicketCommand(
                "VPN outage in Berlin office", "Tunnel drops.", TicketPriority.Low, DateTimeOffset.UtcNow)))
                .IsSuccess.Should().BeTrue();
        }

        using (var scope = provider.CreateScope())
        {
            var tool = new FindSimilarTicketsTool(
                scope.ServiceProvider.GetRequiredService<IHybridTicketSearch>(),
                NullLogger<FindSimilarTicketsTool>.Instance);

            // The only ticket is Low priority; filtering to High must exclude it.
            var input = JsonSerializer.Deserialize<JsonElement>(
                """{ "query": "VPN", "priorities": ["High"] }""");
            var result = await tool.ExecuteAsync(input, CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.Content.Should().NotContain("VPN outage in Berlin office");
        }
    }
}
