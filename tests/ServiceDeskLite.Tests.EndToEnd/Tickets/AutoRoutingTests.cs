using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Agents.Seeding;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.Routing;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Tickets;

/// <summary>
/// Auto-routing (#159) across both persistence providers. A high-confidence decision is
/// applied through the existing update/assign/change-status handlers — so the category,
/// priority, assignee and status persist and every change lands in the audit trail with
/// the assistant actor. A low-confidence decision is returned as a suggestion only and
/// leaves the ticket untouched.
/// </summary>
public sealed class AutoRoutingTests
{
    private const string AssistantActor = "ai-assistant";

    private static async Task SeedRosterAsync(TestServiceProvider host)
    {
        using var scope = host.CreateScope();
        var agents = scope.ServiceProvider.GetRequiredService<IAgentRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        foreach (var agent in AgentRoster.Seed)
            await agents.AddAsync(agent);
        await uow.SaveChangesAsync();
    }

    private static async Task<TicketId> CreateAsync(TestServiceProvider host, string title, string description)
    {
        using var scope = host.CreateScope();
        var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
        var result = await create.HandleAsync(new CreateTicketCommand(
            title, description, TicketPriority.Medium, DateTimeOffset.UtcNow));
        return result.Value!.Id;
    }

    private static async Task<TicketListItemDto> ReadAsync(TestServiceProvider host, TicketId id)
    {
        using var scope = host.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
        var page = await search.HandleAsync(new SearchTicketsQuery(new TicketSearchCriteria(), Paging.Default));
        return page.Value!.Page.Items.Single(t => t.Id == id);
    }

    [Theory]
    [ProviderMatrix]
    public async Task High_confidence_routing_applies_and_audits(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);
        await SeedRosterAsync(host);
        var ticketId = await CreateAsync(host, "VPN outage", "The VPN is down for everyone, nobody can connect.");

        RouteTicketResult routing;
        using (var scope = host.CreateScope())
        {
            var route = scope.ServiceProvider.GetRequiredService<RouteTicketHandler>();
            var result = await route.HandleAsync(new RouteTicketCommand(ticketId, AssistantActor));
            result.IsSuccess.Should().BeTrue();
            routing = result.Value!;
        }

        routing.Applied.Should().BeTrue();
        routing.Decision.Category.Should().Be(TicketCategory.Network);
        routing.Decision.Priority.Should().Be(TicketPriority.Critical);
        routing.Decision.SuggestedAssignee.Should().Be("Alex Kim");

        // Persisted through the command handlers on the chosen provider.
        var item = await ReadAsync(host, ticketId);
        item.Category.Should().Be(TicketCategory.Network);
        item.Priority.Should().Be(TicketPriority.Critical);
        item.Status.Should().Be(TicketStatus.Triaged);
        item.Assignee.Should().Be("Alex Kim");

        using (var scope = host.CreateScope())
        {
            var audit = scope.ServiceProvider.GetRequiredService<IAuditEventRepository>();
            var events = await audit.GetByTicketIdAsync(ticketId);
            // Update (priority+category), assignment, and status change all audited as the assistant.
            events.Where(e => e.Actor == AssistantActor).Should().HaveCountGreaterThanOrEqualTo(3);
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Low_confidence_routing_suggests_without_touching_the_ticket(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);
        await SeedRosterAsync(host);
        var ticketId = await CreateAsync(host, "Follow up", "I wanted to talk about a thing from our meeting.");

        RouteTicketResult routing;
        using (var scope = host.CreateScope())
        {
            var route = scope.ServiceProvider.GetRequiredService<RouteTicketHandler>();
            routing = (await route.HandleAsync(new RouteTicketCommand(ticketId, AssistantActor))).Value!;
        }

        routing.Applied.Should().BeFalse();
        routing.Decision.Confidence.Should().BeLessThan(RouteTicketHandler.ApplyThreshold);
        routing.AppliedChanges.Should().BeEmpty();

        // Ticket is untouched: still uncategorized, New, unassigned.
        var item = await ReadAsync(host, ticketId);
        item.Category.Should().Be(TicketCategory.Uncategorized);
        item.Status.Should().Be(TicketStatus.New);
        item.Assignee.Should().BeNull();
    }
}
