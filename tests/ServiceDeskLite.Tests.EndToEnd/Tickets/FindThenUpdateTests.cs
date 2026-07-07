using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Application.Tickets.UpdateTicket;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Tickets;

/// <summary>
/// Mirrors the assistant's find-then-update flow across both persistence providers:
/// resolve a pre-existing ticket via SearchTicketsHandler, then apply a partial update
/// through UpdateTicketHandler with the assistant actor. Verifies the change persists,
/// unspecified fields stay untouched, and the edit is audited as "ai-assistant".
/// </summary>
public sealed class FindThenUpdateTests
{
    private const string AssistantActor = "ai-assistant";

    [Theory]
    [ProviderMatrix]
    public async Task Find_then_update_applies_partial_change_and_audits_assistant(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        // A ticket the user did not create in-conversation — only describes later.
        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            await create.HandleAsync(TicketFactory.Command(title: "Login page returns 500", priority: TicketPriority.Medium));
            await create.HandleAsync(TicketFactory.Command(title: "Printer offline", priority: TicketPriority.Low));
        }

        // Resolve the description to an id (what search_tickets does), then update by that id.
        TicketId resolvedId;
        using (var scope = host.CreateScope())
        {
            var search = scope.ServiceProvider.GetRequiredService<SearchTicketsHandler>();
            var found = await search.HandleAsync(new SearchTicketsQuery(
                new TicketSearchCriteria(Text: "Login"),
                Paging.Default));

            found.IsSuccess.Should().BeTrue();
            resolvedId = found.Value!.Page.Items.Should().ContainSingle().Subject.Id;

            var update = scope.ServiceProvider.GetRequiredService<UpdateTicketHandler>();
            var result = await update.HandleAsync(new UpdateTicketCommand(
                Id: resolvedId,
                Priority: TicketPriority.High,
                Actor: AssistantActor));

            result.IsSuccess.Should().BeTrue();
        }

        using (var scope = host.CreateScope())
        {
            var get = scope.ServiceProvider.GetRequiredService<GetTicketByIdHandler>();
            var ticket = await get.HandleAsync(new GetTicketByIdQuery(resolvedId));

            ticket.IsSuccess.Should().BeTrue();
            ticket.Value!.Priority.Should().Be(TicketPriority.High);
            ticket.Value.Title.Should().Be("Login page returns 500", "partial update must not touch other fields");

            var audit = scope.ServiceProvider.GetRequiredService<IAuditEventRepository>();
            var events = await audit.GetByTicketIdAsync(resolvedId);
            events.Should().Contain(e => e.Actor == AssistantActor,
                "assistant edits must be attributed to the ai-assistant actor");
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Update_unknown_id_returns_not_found(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        using var scope = host.CreateScope();
        var update = scope.ServiceProvider.GetRequiredService<UpdateTicketHandler>();

        var result = await update.HandleAsync(new UpdateTicketCommand(
            Id: TicketId.New(),
            Priority: TicketPriority.High,
            Actor: AssistantActor));

        result.IsSuccess.Should().BeFalse("a resolved id that no longer exists must surface as a failure the model can relay");
    }
}
