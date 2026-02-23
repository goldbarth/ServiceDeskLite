using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.ChangeTicketStatus;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Tickets;

public sealed class ChangeTicketStatusTests
{
    [Theory]
    [ProviderMatrix]
    public async Task ChangeStatus_ValidTransition_StatusPersistedAcrossScopes(PersistenceProvider provider)
    {
        using var host = TestServiceProvider.Create(provider);

        TicketId ticketId;

        // Scope 1: Create ticket (starts as New)
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            var result = await handler.HandleAsync(TicketFactory.Command());

            result.IsSuccess.Should().BeTrue();
            ticketId = result.Value!.Id;
        }

        // Scope 2: Change status New → Triaged
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<ChangeTicketStatusHandler>();
            var cmd = new ChangeTicketStatusCommand(ticketId, TicketStatus.Triaged);
            var result = await handler.HandleAsync(cmd);

            result.IsSuccess.Should().BeTrue();
            result.Value!.Status.Should().Be(TicketStatus.Triaged);
        }

        // Scope 3: Verify status persisted
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<GetTicketByIdHandler>();
            var result = await handler.HandleAsync(new GetTicketByIdQuery(ticketId));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Status.Should().Be(TicketStatus.Triaged);
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task ChangeStatus_InvalidTransition_ReturnsConflict(PersistenceProvider provider)
    {
        using var host = TestServiceProvider.Create(provider);

        TicketId ticketId;

        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            var result = await handler.HandleAsync(TicketFactory.Command());
            ticketId = result.Value!.Id;
        }

        // New → Closed is not an allowed transition
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<ChangeTicketStatusHandler>();
            var cmd = new ChangeTicketStatusCommand(ticketId, TicketStatus.Closed);
            var result = await handler.HandleAsync(cmd);

            result.IsFailure.Should().BeTrue();
            result.Error!.Type.Should().Be(ErrorType.Conflict);
            result.Error.Code.Should().Be("domain.ticket.status.invalid_transition");
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task ChangeStatus_InvalidTransition_OriginalStatusUnchanged(PersistenceProvider provider)
    {
        using var host = TestServiceProvider.Create(provider);

        TicketId ticketId;

        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            var result = await handler.HandleAsync(TicketFactory.Command());
            ticketId = result.Value!.Id;
        }

        // Attempt invalid transition
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<ChangeTicketStatusHandler>();
            await handler.HandleAsync(new ChangeTicketStatusCommand(ticketId, TicketStatus.Closed));
        }

        // Status must still be New
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<GetTicketByIdHandler>();
            var result = await handler.HandleAsync(new GetTicketByIdQuery(ticketId));

            result.Value!.Status.Should().Be(TicketStatus.New);
        }
    }
}
