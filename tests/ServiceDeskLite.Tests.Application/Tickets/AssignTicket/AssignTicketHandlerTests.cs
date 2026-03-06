using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Application.Tickets.AssignTicket;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Application.Tickets.AssignTicket;

public sealed class AssignTicketHandlerTests
{
    // -----------------------------------------------------------------------
    // Guard: input validation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_NullCommand_ReturnsValidationFailure()
    {
        var handler = CreateHandler(existingTicket: null);

        var result = await handler.HandleAsync(null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("assign_ticket.command.null");
    }

    // -----------------------------------------------------------------------
    // Not Found
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_TicketNotFound_ReturnsNotFound()
    {
        var handler = CreateHandler(existingTicket: null);
        var cmd = new AssignTicketCommand(TicketId.New(), "Alice");

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("ticket.not_found");
    }

    // -----------------------------------------------------------------------
    // Success paths
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ValidAssigneeName_ReturnsSuccessWithAssignee()
    {
        var ticket = CreateTicket();
        var uow = new FakeUnitOfWork();
        var handler = CreateHandler(existingTicket: ticket, uow: uow);
        var cmd = new AssignTicketCommand(ticket.Id, "Alice");

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Assignee!.Value.Name.Should().Be("Alice");
        uow.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_NullAssigneeName_ReturnsSuccessWithNullAssignee()
    {
        var ticket = CreateTicket();
        ticket.Assign(new Assignee("Alice"));
        ticket.ClearDomainEvents(); // clear setup event so only the unassign event remains
        var handler = CreateHandler(existingTicket: ticket);
        var cmd = new AssignTicketCommand(ticket.Id, AssigneeName: null);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Assignee.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ValidAssign_TicketAssigneeIsUpdated()
    {
        var ticket = CreateTicket();
        var handler = CreateHandler(existingTicket: ticket);
        var cmd = new AssignTicketCommand(ticket.Id, "Bob");

        await handler.HandleAsync(cmd);

        ticket.Assignee!.Value.Name.Should().Be("Bob");
    }

    // -----------------------------------------------------------------------
    // Domain violations
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ClosedTicket_ReturnsConflict()
    {
        var ticket = CreateClosedTicket();
        var handler = CreateHandler(existingTicket: ticket);
        var cmd = new AssignTicketCommand(ticket.Id, "Alice");

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("domain.ticket.assign.closed");
    }

    [Fact]
    public async Task HandleAsync_AssigneeNameTooLong_ReturnsValidationFailure()
    {
        var ticket = CreateTicket();
        var handler = CreateHandler(existingTicket: ticket, validator: new AssignTicketValidator());
        var tooLong = new string('x', Assignee.MaxNameLength + 1);
        var cmd = new AssignTicketCommand(ticket.Id, tooLong);

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("assign_ticket.validation_failed");
        result.Error.FieldErrors.Should().ContainKey("assigneeName");
    }

    [Fact]
    public async Task HandleAsync_EmptyAssigneeName_ReturnsValidationFailure()
    {
        var ticket = CreateTicket();
        var handler = CreateHandler(existingTicket: ticket, validator: new AssignTicketValidator());
        var cmd = new AssignTicketCommand(ticket.Id, AssigneeName: "");

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("assign_ticket.validation_failed");
        result.Error.FieldErrors.Should().ContainKey("assigneeName");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static AssignTicketHandler CreateHandler(
        Ticket? existingTicket,
        FakeUnitOfWork? uow = null,
        ICommandValidator<AssignTicketCommand>? validator = null)
    {
        var repo = new FakeTicketRepository(existingTicket);
        return new AssignTicketHandler(repo, new FakeAuditEventRepository(), uow ?? new FakeUnitOfWork(), validator ?? new FakeValidator());
    }

    private static Ticket CreateTicket() =>
        new(TicketId.New(), "Cannot login", "User reports login fails.", TicketPriority.Medium, DateTimeOffset.UtcNow);

    private static Ticket CreateClosedTicket()
    {
        var ticket = CreateTicket();
        ticket.ChangeStatus(TicketStatus.Triaged);
        ticket.ChangeStatus(TicketStatus.Resolved);
        ticket.ChangeStatus(TicketStatus.Closed);
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
    
    private sealed class FakeAuditEventRepository : IAuditEventRepository
    {
        public Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<AuditEvent>> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
    
    private sealed class FakeValidator : ICommandValidator<AssignTicketCommand>
    {
        public FieldValidationResult Validate(AssignTicketCommand command) => FieldValidationResult.Ok;
    }
}
