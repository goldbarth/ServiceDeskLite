using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Tickets.AssignTicket;
using ServiceDeskLite.Application.Tickets.ChangeTicketStatus;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Agents;
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

    // Assignee filtering now translates on Postgres: the filter joins the agent roster
    // (ADR-0025) instead of the old value-converted Assignee column, so the #144 EF
    // translation bug no longer applies. Case-insensitive coverage lives in
    // Filter_by_assignee_is_case_insensitive below (ADR-0042).

    [Theory]
    [ProviderMatrix]
    public async Task Filter_unassigned_returns_only_tickets_without_agent(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        var agent = new Agent(
            AgentId.New(), "Alex Kim", "alex.kim@servicedesk.example");

        using (var scope = host.CreateScope())
        {
            var agents = scope.ServiceProvider.GetRequiredService<IAgentRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await agents.AddAsync(agent);
            await uow.SaveChangesAsync();
        }

        TicketId assignedId;
        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            assignedId = (await create.HandleAsync(TicketFactory.Command(title: "Assigned one"))).Value!.Id;
            await create.HandleAsync(TicketFactory.Command(title: "Unassigned one"));
        }

        using (var scope = host.CreateScope())
        {
            var assign = scope.ServiceProvider.GetRequiredService<AssignTicketHandler>();
            (await assign.HandleAsync(new AssignTicketCommand(
                assignedId, agent.Id, "test"))).IsSuccess.Should().BeTrue();
        }

        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var result = await search.HandleAsync(new SearchTicketsQuery(
                new TicketSearchCriteria(Unassigned: true),
                Paging.Default));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Page.Items.Should().ContainSingle()
                .Which.Title.Should().Be("Unassigned one");
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Filter_overdue_returns_only_open_tickets_past_due(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
        var tomorrow = DateTimeOffset.UtcNow.AddDays(1);

        TicketId resolvedId;
        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            await create.HandleAsync(TicketFactory.Command(title: "Overdue and open", dueAt: yesterday));
            await create.HandleAsync(TicketFactory.Command(title: "Due tomorrow", dueAt: tomorrow));
            await create.HandleAsync(TicketFactory.Command(title: "No due date"));
            resolvedId = (await create.HandleAsync(TicketFactory.Command(title: "Overdue but resolved", dueAt: yesterday))).Value!.Id;
        }

        // Past due but no longer open must not count as overdue - same rule as IsOverdue.
        using (var scope = host.CreateScope())
        {
            var change = scope.ServiceProvider.GetRequiredService<ChangeTicketStatusHandler>();
            (await change.HandleAsync(new ChangeTicketStatusCommand(
                resolvedId, TicketStatus.Triaged))).IsSuccess.Should().BeTrue();
            (await change.HandleAsync(new ChangeTicketStatusCommand(
                resolvedId, TicketStatus.Resolved))).IsSuccess.Should().BeTrue();
        }

        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var result = await search.HandleAsync(new SearchTicketsQuery(
                new TicketSearchCriteria(Overdue: true),
                Paging.Default));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Page.Items.Should().ContainSingle()
                .Which.Title.Should().Be("Overdue and open");
            result.Value.Page.Items.Should().OnlyContain(t => t.IsOverdue);
        }
    }

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

    // ADR-0042: free-text search matches case-insensitively on both providers.
    [Theory]
    [ProviderMatrix]
    public async Task Filter_by_free_text_is_case_insensitive(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            await create.HandleAsync(TicketFactory.Command(title: "Remote access broken"));
            await create.HandleAsync(TicketFactory.Command(title: "Printer offline", description: "user cannot print REMOTELY"));
            await create.HandleAsync(TicketFactory.Command(title: "Keyboard sticky"));
        }

        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var result = await search.HandleAsync(new SearchTicketsQuery(
                new TicketSearchCriteria(Text: "remote"),
                Paging.Default));

            result.IsSuccess.Should().BeTrue();
            // Lower-case query hits a title-cased title AND an upper-cased description.
            result.Value!.Page.Items.Should().HaveCount(2);
            result.Value.Page.Items.Should().Contain(t => t.Title == "Remote access broken");
            result.Value.Page.Items.Should().Contain(t => t.Title == "Printer offline");
        }
    }

    // ADR-0042: the assignee filter folds case the same way as free-text search.
    [Theory]
    [ProviderMatrix]
    public async Task Filter_by_assignee_is_case_insensitive(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        var agent = new Agent(AgentId.New(), "Alex Kim", "alex.kim@servicedesk.example");

        using (var scope = host.CreateScope())
        {
            var agents = scope.ServiceProvider.GetRequiredService<IAgentRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await agents.AddAsync(agent);
            await uow.SaveChangesAsync();
        }

        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            var assignedId = (await create.HandleAsync(TicketFactory.Command(title: "Alex ticket"))).Value!.Id;
            await create.HandleAsync(TicketFactory.Command(title: "Unassigned"));

            var assign = scope.ServiceProvider.GetRequiredService<AssignTicketHandler>();
            (await assign.HandleAsync(new AssignTicketCommand(assignedId, agent.Id, "test")))
                .IsSuccess.Should().BeTrue();
        }

        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var result = await search.HandleAsync(new SearchTicketsQuery(
                new TicketSearchCriteria(AssigneeName: "alex"),
                Paging.Default));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Page.Items.Should().ContainSingle()
                .Which.Title.Should().Be("Alex ticket");
        }
    }
}
