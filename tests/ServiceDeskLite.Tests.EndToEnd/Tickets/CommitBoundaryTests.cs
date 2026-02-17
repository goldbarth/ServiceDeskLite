using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Tests.EndToEnd.Composition;

namespace ServiceDeskLite.Tests.EndToEnd.Tickets;

public sealed class CommitBoundaryTests
{
    [Theory]
    [ProviderMatrix]
    public async Task Created_ticket_is_visible_after_commit_in_new_scope(PersistenceProvider provider)
    {
        using var host = TestServiceProvider.Create(provider);

        TicketId ticketId;

        // Scope 1: Create ticket (handler commits internally)
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<CreateTicketHandler>();
            var result = await handler.HandleAsync(TicketFactory.Command());

            result.IsSuccess.Should().BeTrue();
            ticketId = result.Value!.Id;
        }

        // Scope 2: Ticket must be visible
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<GetTicketByIdHandler>();
            var result = await handler.HandleAsync(new GetTicketByIdQuery(ticketId));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Id.Should().Be(ticketId);
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Add_without_commit_is_not_visible_in_other_scope(PersistenceProvider provider)
    {
        using var host = TestServiceProvider.Create(provider);

        TicketId ticketId;

        // Scope 1: Add ticket via repository but do NOT commit
        using (var scope = host.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
            var ticket = new Ticket(
                TicketId.New(), "Uncommitted", "Should not be visible",
                TicketPriority.Low, DateTimeOffset.UtcNow);
            ticketId = ticket.Id;

            await repo.AddAsync(ticket);
            // Deliberately NOT calling UnitOfWork.SaveChangesAsync
        }

        // Scope 2: Ticket must NOT be visible
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<GetTicketByIdHandler>();
            var result = await handler.HandleAsync(new GetTicketByIdQuery(ticketId));

            result.IsFailure.Should().BeTrue();
            result.Error!.Type.Should().Be(ErrorType.NotFound);
        }
    }

    [Theory]
    [ProviderMatrix]
    public async Task Add_with_commit_is_visible_in_other_scope(PersistenceProvider provider)
    {
        using var host = TestServiceProvider.Create(provider);

        TicketId ticketId;

        // Scope 1: Add + commit via low-level ports
        using (var scope = host.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var ticket = new Ticket(
                TicketId.New(), "Committed", "Should be visible",
                TicketPriority.High, DateTimeOffset.UtcNow);
            ticketId = ticket.Id;

            await repo.AddAsync(ticket);
            await uow.SaveChangesAsync();
        }

        // Scope 2: Ticket must be visible
        using (var scope = host.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<GetTicketByIdHandler>();
            var result = await handler.HandleAsync(new GetTicketByIdQuery(ticketId));

            result.IsSuccess.Should().BeTrue();
            result.Value!.Id.Should().Be(ticketId);
        }
    }
}
