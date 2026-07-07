using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Application.Tickets.AssignTicket;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Agents;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Application.Tickets.AssignTicket;

public sealed class AssignTicketHandlerTests
{
    private static readonly Agent Alice =
        new(AgentId.New(), "Alice", "alice@servicedesk.example");
    private static readonly Agent Bob =
        new(AgentId.New(), "Bob", "bob@servicedesk.example");
    private static readonly Agent Inactive =
        new(AgentId.New(), "Retired", "retired@servicedesk.example", active: false);

    [Fact]
    public async Task HandleAsync_NullCommand_ReturnsValidationFailure()
    {
        var handler = CreateHandler(existingTicket: null);

        var result = await handler.HandleAsync(null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("assign_ticket.command.null");
    }

    [Fact]
    public async Task HandleAsync_TicketNotFound_ReturnsNotFound()
    {
        var handler = CreateHandler(existingTicket: null);
        var cmd = new AssignTicketCommand(TicketId.New(), Alice.Id);

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("ticket.not_found");
    }

    [Fact]
    public async Task HandleAsync_ValidAgent_ReturnsSuccessWithAssigneeName()
    {
        var ticket = CreateTicket();
        var uow = new FakeUnitOfWork();
        var handler = CreateHandler(existingTicket: ticket, uow: uow);
        var cmd = new AssignTicketCommand(ticket.Id, Alice.Id);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Assignee.Should().Be("Alice");
        ticket.AssignedAgentId.Should().Be(Alice.Id);
        uow.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_NullAgent_ReturnsSuccessWithNullAssignee()
    {
        var ticket = CreateTicket();
        ticket.Assign(Alice.Id, "Alice", previousAssigneeName: null);
        ticket.ClearDomainEvents();
        var handler = CreateHandler(existingTicket: ticket);
        var cmd = new AssignTicketCommand(ticket.Id, AgentId: null);

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Assignee.Should().BeNull();
        ticket.AssignedAgentId.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_UnknownAgent_ReturnsValidationFailure()
    {
        var ticket = CreateTicket();
        var handler = CreateHandler(existingTicket: ticket);
        var cmd = new AssignTicketCommand(ticket.Id, AgentId.New());

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("assign_ticket.agent.unknown");
    }

    [Fact]
    public async Task HandleAsync_InactiveAgent_ReturnsValidationFailure()
    {
        var ticket = CreateTicket();
        var handler = CreateHandler(existingTicket: ticket);
        var cmd = new AssignTicketCommand(ticket.Id, Inactive.Id);

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("assign_ticket.agent.unknown");
    }

    [Fact]
    public async Task HandleAsync_ClosedTicket_ReturnsConflict()
    {
        var ticket = CreateClosedTicket();
        var handler = CreateHandler(existingTicket: ticket);
        var cmd = new AssignTicketCommand(ticket.Id, Alice.Id);

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("domain.ticket.assign.closed");
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
        var agents = new FakeAgentRepository(Alice, Bob, Inactive);
        return new AssignTicketHandler(
            repo, agents, new FakeAuditEventRepository(),
            uow ?? new FakeUnitOfWork(), validator ?? new FakeValidator(), new FakeClock());
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

        public Task<PagedResult<TicketListItemDto>> SearchAsync(
            TicketSearchCriteria criteria, Paging paging, SortSpec sort,
            CancellationToken ct = default)
            => Task.FromResult(new PagedResult<TicketListItemDto>([], 0, paging));
    }

    private sealed class FakeAgentRepository(params Agent[] agents) : IAgentRepository
    {
        public Task AddAsync(Agent agent, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Agent?> GetByIdAsync(AgentId id, CancellationToken ct = default)
            => Task.FromResult(agents.FirstOrDefault(a => a.Id == id));

        public Task<IReadOnlyList<Agent>> GetActiveAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Agent>>(agents.Where(a => a.Active).ToList());

        public Task<bool> AnyAsync(CancellationToken ct = default)
            => Task.FromResult(agents.Length > 0);
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
            => Task.FromResult<IReadOnlyList<AuditEvent>>([]);
    }

    private sealed class FakeValidator : ICommandValidator<AssignTicketCommand>
    {
        public FieldValidationResult Validate(AssignTicketCommand command) => FieldValidationResult.Ok;
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
