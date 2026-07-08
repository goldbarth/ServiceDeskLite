using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Tickets.AssignTicket;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Agents;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Assistant;

/// <summary>
/// The autonomous duplicate-check → create → assign chain, verified at the application
/// layer across both persistence providers (same approach as AssistantChangeStatusTests):
/// each step runs through the exact command handlers the assistant's tools call, so the
/// chain a single user turn triggers can never bypass domain validation. Covers acceptance
/// criterion #4 without a live model.
/// </summary>
public sealed class AssistantOrchestrationTests
{
    private const string AssistantActor = "ai-assistant";

    private static readonly Agent Alex =
        new(AgentId.New(), "Alex Kim", "alex.kim@servicedesk.example");

    [Theory]
    [ProviderMatrix]
    public async Task DuplicateCheck_create_assign_chain_completes(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        using (var scope = host.CreateScope())
        {
            var agents = scope.ServiceProvider.GetRequiredService<IAgentRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await agents.AddAsync(Alex);
            await uow.SaveChangesAsync();
        }

        // Step 1 — duplicate check: nothing exists yet, so the chain proceeds to create.
        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var duplicates = await search.HandleAsync(
                new SearchTicketsQuery(new TicketSearchCriteria(Text: "VPN outage"), Paging.Default));

            duplicates.Value!.Page.Items.Should().BeEmpty("no duplicate exists — the chain should create");
        }

        // Step 2 — create the new ticket.
        TicketId ticketId;
        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            ticketId = (await create.HandleAsync(TicketFactory.Command(title: "VPN outage"))).Value!.Id;
        }

        // Step 3 — assign it, resolving the created ticket id from the previous step.
        using (var scope = host.CreateScope())
        {
            var assign = scope.ServiceProvider.GetRequiredService<AssignTicketHandler>();
            var result = await assign.HandleAsync(new AssignTicketCommand(ticketId, Alex.Id, AssistantActor));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Assignee.Should().Be("Alex Kim");
        }

        // The chain's net effect: an assigned ticket, audited as the assistant.
        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var page = await search.HandleAsync(new SearchTicketsQuery(new TicketSearchCriteria(), Paging.Default));
            page.Value!.Page.Items.Single(t => t.Id == ticketId).Assignee.Should().Be("Alex Kim");

            var audit = scope.ServiceProvider.GetRequiredService<IAuditEventRepository>();
            var events = await audit.GetByTicketIdAsync(ticketId);
            events.Should().Contain(e => e.Actor == AssistantActor);
        }
    }
}
