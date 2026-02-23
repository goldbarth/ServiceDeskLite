using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.ChangeTicketStatus;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Application.Tickets.ChangeTicketStatus;

public class ChangeTicketStatusHandlerTests
{
    [Fact]
    public async Task HandleAsync_NullCommand_ReturnsValidationFailure()
    {
        var handler = CreateHandler(existingTicket: null);

        var result = await handler.HandleAsync(null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("change_ticket_status.command.null");
    }

    [Fact]
    public async Task HandleAsync_TicketNotFound_ReturnsNotFound()
    {
        var handler = CreateHandler(existingTicket: null);
        var cmd = new ChangeTicketStatusCommand(TicketId.New(), TicketStatus.Triaged);

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("ticket.not_found");
    }

    [Fact]
    public async Task HandleAsync_InvalidTransition_ReturnsConflict()
    {
        // New → Closed is not an allowed transition
        var ticket = CreateTicket(TicketStatus.New);
        var handler = CreateHandler(existingTicket: ticket);
        var cmd = new ChangeTicketStatusCommand(ticket.Id, TicketStatus.Closed);

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("domain.ticket.status.invalid_transition");
    }

    [Fact]
    public async Task HandleAsync_ValidTransition_ReturnsSuccess()
    {
        // New → Triaged is valid
        var ticket = CreateTicket(TicketStatus.New);
        var uow = new FakeUnitOfWork();
        var handler = CreateHandler(existingTicket: ticket, uow: uow);
        var cmd = new ChangeTicketStatusCommand(ticket.Id, TicketStatus.Triaged);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(TicketStatus.Triaged);
        uow.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_ValidTransition_TicketStatusIsUpdated()
    {
        var ticket = CreateTicket(TicketStatus.Triaged);
        var handler = CreateHandler(existingTicket: ticket);
        var cmd = new ChangeTicketStatusCommand(ticket.Id, TicketStatus.InProgress);

        await handler.HandleAsync(cmd);

        ticket.Status.Should().Be(TicketStatus.InProgress);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ChangeTicketStatusHandler CreateHandler(
        Ticket? existingTicket,
        FakeUnitOfWork? uow = null)
    {
        var repo = new FakeTicketRepository(existingTicket);
        return new ChangeTicketStatusHandler(repo, uow ?? new FakeUnitOfWork());
    }

    private static Ticket CreateTicket(TicketStatus status)
    {
        var ticket = new Ticket(
            TicketId.New(),
            "Test Ticket",
            "Description",
            TicketPriority.Medium,
            DateTimeOffset.UtcNow);

        // Advance to the desired status by following allowed transitions
        if (status == TicketStatus.Triaged)
            ticket.ChangeStatus(TicketStatus.Triaged);
        else if (status == TicketStatus.InProgress)
        {
            ticket.ChangeStatus(TicketStatus.Triaged);
            ticket.ChangeStatus(TicketStatus.InProgress);
        }

        return ticket;
    }

    private sealed class FakeTicketRepository(Ticket? ticket) : ITicketRepository
    {
        public Task AddAsync(Ticket t, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default)
            => Task.FromResult(ticket);

        public Task<bool> ExistsAsync(TicketId id, CancellationToken ct = default)
            => Task.FromResult(ticket is not null);

        public Task<PagedResult<Ticket>> SearchAsync(
            TicketSearchCriteria criteria, Paging paging, SortSpec sort,
            CancellationToken ct = default)
            => Task.FromResult(new PagedResult<Ticket>([], 0, paging));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }
}
