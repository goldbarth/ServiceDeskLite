using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Application.Tickets.CreateTicket;

public class CreateTicketHandlerTests
{
    [Fact]
    public async Task Returns_validation_error_when_title_missing()
    {
        var repo = new FakeTicketRepository();
        var uow = new FakeUnitOfWork();
        var handler = new CreateTicketHandler(repo, new FakeAuditEventRepository(), uow, new CreateTicketValidator(), new FakeClock());

        var cmd = new CreateTicketCommand(
            Title: " ",
            Description: "desc",
            Priority: TicketPriority.Medium,
            CreatedAt: DateTimeOffset.UtcNow);

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("create_ticket.validation_failed");
        result.Error.FieldErrors.Should().ContainKey("title");
        uow.SaveCalls.Should().Be(0);
        repo.AddCalls.Should().Be(0);
    }

    [Fact]
    public async Task Returns_conflict_error_when_duplicate_ticket()
    {
        var repo = new FakeTicketRepository();
        var uow = new FakeUnitOfWork(
            new InvalidOperationException("Ticket already exists: 123"));
        var handler = new CreateTicketHandler(repo, new FakeAuditEventRepository(), uow, new FakeValidator(), new FakeClock());

        var cmd = new CreateTicketCommand(
            Title: "Test",
            Description: "desc",
            Priority: TicketPriority.Medium,
            CreatedAt: DateTimeOffset.UtcNow);

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("persistence.conflict");
    }

    [Fact]
    public async Task Returns_unexpected_error_when_save_throws_unknown_exception()
    {
        var repo = new FakeTicketRepository();
        var uow = new FakeUnitOfWork(new IOException("disk full"));
        var handler = new CreateTicketHandler(repo, new FakeAuditEventRepository(), uow, new FakeValidator(), new FakeClock());

        var cmd = new CreateTicketCommand(
            Title: "Test",
            Description: "desc",
            Priority: TicketPriority.Medium,
            CreatedAt: DateTimeOffset.UtcNow);

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unexpected);
        result.Error.Code.Should().Be("persistence.unexpected");
    }

    private sealed class FakeTicketRepository : ITicketRepository
    {
        public int AddCalls { get; private set; }

        public Task AddAsync(Ticket ticket, CancellationToken ct = default)
        {
            AddCalls++;
            return Task.CompletedTask;
        }

        public Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default)
            => Task.FromResult<Ticket?>(null);

        public Task<bool> ExistsAsync(TicketId id, CancellationToken ct)
            => Task.FromResult(false);

        public Task<PagedResult<TicketListItemDto>> SearchAsync(
            TicketSearchCriteria criteria,
            Paging paging,
            SortSpec sort,
            CancellationToken ct = default)
            => Task.FromResult(new PagedResult<TicketListItemDto>([], 0, paging));
    }
    
    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        private readonly Exception? _exceptionToThrow;

        public FakeUnitOfWork(Exception? exceptionToThrow = null)
            => _exceptionToThrow = exceptionToThrow;

        public int SaveCalls { get; private set; }

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCalls++;

            if (_exceptionToThrow is not null)
                throw _exceptionToThrow;

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

    private sealed class FakeValidator : ICommandValidator<CreateTicketCommand>
    {
        public FieldValidationResult Validate(CreateTicketCommand command) => FieldValidationResult.Ok;
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
