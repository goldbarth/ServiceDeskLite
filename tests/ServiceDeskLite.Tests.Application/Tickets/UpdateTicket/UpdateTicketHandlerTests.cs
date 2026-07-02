using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Application.Tickets.UpdateTicket;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Application.Tickets.UpdateTicket;

public class UpdateTicketHandlerTests
{
    [Fact]
    public async Task HandleAsync_NullCommand_ReturnsValidationFailure()
    {
        var handler = CreateHandler(existingTicket: null);

        var result = await handler.HandleAsync(null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("update_ticket.command.null");
    }

    [Fact]
    public async Task HandleAsync_NoFieldsProvided_ReturnsValidationFailure()
    {
        var handler = CreateHandler(existingTicket: CreateTicket());
        var cmd = new UpdateTicketCommand(TicketId.New());

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("update_ticket.validation_failed");
    }

    [Fact]
    public async Task HandleAsync_TitleTooLong_ReturnsValidationFailure()
    {
        var handler = CreateHandler(existingTicket: CreateTicket());
        var cmd = new UpdateTicketCommand(TicketId.New(), Title: new string('x', Ticket.MaxTitleLength + 1));

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task HandleAsync_TicketNotFound_ReturnsNotFound()
    {
        var handler = CreateHandler(existingTicket: null);
        var cmd = new UpdateTicketCommand(TicketId.New(), Title: "New title");

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("ticket.not_found");
    }

    [Fact]
    public async Task HandleAsync_UpdatesFields_AndSaves()
    {
        var ticket = CreateTicket();
        var uow = new FakeUnitOfWork();
        var audit = new FakeAuditEventRepository();
        var handler = CreateHandler(existingTicket: ticket, uow: uow, audit: audit);
        var newDue = new DateTimeOffset(2026, 7, 3, 9, 0, 0, TimeSpan.FromHours(2));
        var cmd = new UpdateTicketCommand(
            ticket.Id, Priority: TicketPriority.Critical, DueAt: newDue, Actor: "ai-assistant");

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        ticket.Priority.Should().Be(TicketPriority.Critical);
        ticket.DueAt.Should().Be(newDue);
        uow.SaveCalls.Should().Be(1);
        audit.Added.Should().ContainSingle(e => e.EventType == AuditEventTypes.DetailsUpdated && e.Actor == "ai-assistant");
    }

    [Fact]
    public async Task HandleAsync_NoOpUpdate_SucceedsWithoutSaving()
    {
        var ticket = CreateTicket();
        var uow = new FakeUnitOfWork();
        var handler = CreateHandler(existingTicket: ticket, uow: uow);
        var cmd = new UpdateTicketCommand(ticket.Id, Title: ticket.Title);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        uow.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_ClosedTicket_ReturnsConflict()
    {
        var ticket = CreateTicket();
        ticket.ChangeStatus(TicketStatus.Triaged);
        ticket.ChangeStatus(TicketStatus.InProgress);
        ticket.ChangeStatus(TicketStatus.Resolved);
        ticket.ChangeStatus(TicketStatus.Closed);
        ticket.ClearDomainEvents();

        var handler = CreateHandler(existingTicket: ticket);
        var cmd = new UpdateTicketCommand(ticket.Id, Title: "New title");

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be(TicketErrors.CannotUpdateClosedCode);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static UpdateTicketHandler CreateHandler(
        Ticket? existingTicket,
        FakeUnitOfWork? uow = null,
        FakeAuditEventRepository? audit = null)
    {
        var repo = new FakeTicketRepository(existingTicket);
        return new UpdateTicketHandler(
            repo,
            audit ?? new FakeAuditEventRepository(),
            uow ?? new FakeUnitOfWork(),
            new UpdateTicketValidator(),
            new FakeClock());
    }

    private static Ticket CreateTicket()
    {
        var ticket = new Ticket(
            TicketId.New(),
            "Test Ticket",
            "Description",
            TicketPriority.Medium,
            DateTimeOffset.UtcNow);

        ticket.ClearDomainEvents();
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

        public Task<PagedResult<TicketListItemDto>> SearchAsync(
            TicketSearchCriteria criteria, Paging paging, SortSpec sort,
            CancellationToken ct = default)
            => Task.FromResult(new PagedResult<TicketListItemDto>([], 0, paging));
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

    private sealed class FakeAuditEventRepository : IAuditEventRepository
    {
        public List<AuditEvent> Added { get; } = [];

        public Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
        {
            Added.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEvent>> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AuditEvent>>(Added.AsReadOnly());
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);
    }
}
