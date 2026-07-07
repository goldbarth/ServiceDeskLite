using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.ChangeTicketStatus;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Tickets;

/// <summary>
/// Mirrors the change_ticket_status tool's action across both persistence providers:
/// the tool maps to a ChangeTicketStatusCommand with actor "ai-assistant" and runs it
/// through ChangeTicketStatusHandler. Verifies a valid transition persists and is
/// audited as the assistant, and that the domain rejects an invalid transition so the
/// failure can be relayed to the model.
/// </summary>
public sealed class AssistantChangeStatusTests
{
    private const string AssistantActor = "ai-assistant";

    [Theory]
    [ProviderMatrix]
    public async Task Valid_transition_persists_and_audits_assistant(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        TicketId ticketId;
        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            ticketId = (await create.HandleAsync(TicketFactory.Command())).Value!.Id;
        }

        using (var scope = host.CreateScope())
        {
            var change = scope.ServiceProvider.GetRequiredService<ChangeTicketStatusHandler>();
            var result = await change.HandleAsync(
                new ChangeTicketStatusCommand(ticketId, TicketStatus.Triaged, AssistantActor));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Status.Should().Be(TicketStatus.Triaged);
        }

        using (var scope = host.CreateScope())
        {
            var audit = scope.ServiceProvider.GetRequiredService<IAuditEventRepository>();
            var events = await audit.GetByTicketIdAsync(ticketId);
            events.Should().Contain(e => e.Actor == AssistantActor,
                "assistant status changes must be attributed to the ai-assistant actor");
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Invalid_transition_is_rejected_for_relay(PersistenceProvider provider)
    {
        await using var host = await TestServiceProvider.CreateAsync(provider);

        TicketId ticketId;
        using (var scope = host.CreateScope())
        {
            var create = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            ticketId = (await create.HandleAsync(TicketFactory.Command())).Value!.Id;
        }

        using (var scope = host.CreateScope())
        {
            var change = scope.ServiceProvider.GetRequiredService<ChangeTicketStatusHandler>();
            // New → Closed is not an allowed transition.
            var result = await change.HandleAsync(
                new ChangeTicketStatusCommand(ticketId, TicketStatus.Closed, AssistantActor));

            result.IsFailure.Should().BeTrue();
            result.Error!.Type.Should().Be(ErrorType.Conflict);
            result.Error.Message.Should().NotBeNullOrWhiteSpace("the reason is relayed to the model");
        }
    }
}
