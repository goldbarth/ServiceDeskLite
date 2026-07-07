using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.AssignTicket;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Agents;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Tickets;

/// <summary>
/// Assignment against the seeded agent roster (FK model, ADR-0025) across both
/// persistence providers: assign resolves and persists the agent id + snapshots the
/// name into the audit trail; unassign clears it; an unknown/inactive agent is rejected.
/// </summary>
public sealed class AgentAssignmentTests
{
    private const string AssistantActor = "ai-assistant";

    private static readonly Agent Alex =
        new(AgentId.New(), "Alex Kim", "alex.kim@servicedesk.example");
    private static readonly Agent Sam =
        new(AgentId.New(), "Sam Rivera", "sam.rivera@servicedesk.example");
    private static readonly Agent Retired =
        new(AgentId.New(), "Retired Ray", "ray@servicedesk.example", active: false);

    private static async Task SeedAgentsAsync(TestServiceProvider host)
    {
        using var scope = host.CreateScope();
        var agents = scope.ServiceProvider.GetRequiredService<IAgentRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await agents.AddAsync(Alex);
        await agents.AddAsync(Sam);
        await agents.AddAsync(Retired);
        await uow.SaveChangesAsync();
    }

    private static async Task<TicketId> CreateTicketAsync(TestServiceProvider host)
    {
        using var scope = host.CreateScope();
        var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
        return (await create.HandleAsync(TicketFactory.Command())).Value!.Id;
    }

    [Theory]
    [ProviderMatrix]
    public async Task Assign_active_agent_persists_and_audits_name(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);
        await SeedAgentsAsync(host);
        var ticketId = await CreateTicketAsync(host);

        using (var scope = host.CreateScope())
        {
            var assign = scope.ServiceProvider.GetRequiredService<AssignTicketHandler>();
            var result = await assign.HandleAsync(new AssignTicketCommand(ticketId, Alex.Id, AssistantActor));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Assignee.Should().Be("Alex Kim");
        }

        using (var scope = host.CreateScope())
        {
            // The list read model resolves the name via the roster join.
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var page = await search.HandleAsync(new SearchTicketsQuery(new TicketSearchCriteria(), Paging.Default));
            page.Value!.Page.Items.Single(t => t.Id == ticketId).Assignee.Should().Be("Alex Kim");

            // Filtering by assignee name now joins the roster on both providers (fixes #144).
            var filtered = await search.HandleAsync(new SearchTicketsQuery(
                new TicketSearchCriteria(AssigneeName: "Alex"), Paging.Default));
            filtered.Value!.Page.Items.Should().Contain(t => t.Id == ticketId);

            var audit = scope.ServiceProvider.GetRequiredService<IAuditEventRepository>();
            var events = await audit.GetByTicketIdAsync(ticketId);
            events.Should().Contain(e => e.Actor == AssistantActor);
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Reassign_then_unassign_clears_assignment(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);
        await SeedAgentsAsync(host);
        var ticketId = await CreateTicketAsync(host);

        using (var scope = host.CreateScope())
        {
            var assign = scope.ServiceProvider.GetRequiredService<AssignTicketHandler>();
            (await assign.HandleAsync(new AssignTicketCommand(ticketId, Alex.Id, AssistantActor))).IsSuccess.Should().BeTrue();
            (await assign.HandleAsync(new AssignTicketCommand(ticketId, Sam.Id, AssistantActor))).IsSuccess.Should().BeTrue();
            var unassigned = await assign.HandleAsync(new AssignTicketCommand(ticketId, AgentId: null, AssistantActor));

            unassigned.IsSuccess.Should().BeTrue();
            unassigned.Value!.Assignee.Should().BeNull();
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Assign_unknown_agent_is_rejected(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);
        await SeedAgentsAsync(host);
        var ticketId = await CreateTicketAsync(host);

        using var scope = host.CreateScope();
        var assign = scope.ServiceProvider.GetRequiredService<AssignTicketHandler>();

        var result = await assign.HandleAsync(new AssignTicketCommand(ticketId, AgentId.New(), AssistantActor));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("assign_ticket.agent.unknown");
    }

    [Theory]
    [ProviderMatrix]
    public async Task Assign_inactive_agent_is_rejected(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);
        await SeedAgentsAsync(host);
        var ticketId = await CreateTicketAsync(host);

        using var scope = host.CreateScope();
        var assign = scope.ServiceProvider.GetRequiredService<AssignTicketHandler>();

        var result = await assign.HandleAsync(new AssignTicketCommand(ticketId, Retired.Id, AssistantActor));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("assign_ticket.agent.unknown");
    }
}
