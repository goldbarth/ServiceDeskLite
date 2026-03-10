using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Application.Tickets.AddComment;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;
using System.Text.Json;

namespace ServiceDeskLite.Tests.Application.Tickets.AddComment;

public sealed class AddCommentHandlerTest
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
        result.Error.Code.Should().Be("add_comment.command.null");
    }
    
    // -----------------------------------------------------------------------
    // Not Found
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_TicketNotFound_ReturnsNotFound()
    {
        var handler = CreateHandler(existingTicket: null);
        var result = await handler.HandleAsync(
            new AddCommentCommand(TicketId.New(), Content: "test", null, new DateTimeOffset()));
        
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("ticket.not_found");
    }
    
    // -----------------------------------------------------------------------
    // Success paths
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ValidComment_ReturnsSuccessWithComment()
    {
        var ticket = CreateTicket();
        var uow = new FakeUnitOfWork();
        var handler = CreateHandler(ticket, uow);
        var cmd = new AddCommentCommand(
            ticket.Id, 
            Content: "Love Connection.", 
            Author: null, 
            new DateTimeOffset());
        
        
        var result = await handler.HandleAsync(cmd);
        
        result.IsSuccess.Should().BeTrue();
        result.Value!.Comment.Content.Should().Be("Love Connection.");
        ticket.Comments.Should().HaveCount(1);
        uow.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_ValidAuthor_ReturnsSuccessWithAuthor()
    {
        var ticket = CreateTicket();
        var uow = new FakeUnitOfWork();
        var handler = CreateHandler(ticket, uow);
        var cmd = new AddCommentCommand(
            ticket.Id,
            Content: "Love Connection.",
            Author: "Mr. Wonder",
            new DateTimeOffset());
        
        var result = await handler.HandleAsync(cmd);
        result.IsSuccess.Should().BeTrue();
        result.Value!.Comment.Author.Should().Be("Mr. Wonder");
        uow.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_ValidComment_WritesReadableAuditPayload()
    {
        var ticket = CreateTicket();
        var uow = new FakeUnitOfWork();
        var auditRepository = new FakeAuditEventRepository();
        var handler = CreateHandler(ticket, uow, auditRepository);
        var cmd = new AddCommentCommand(
            ticket.Id,
            Content: "Readable history comment.",
            Author: "Mr. Wonder",
            new DateTimeOffset(2026, 03, 09, 10, 30, 00, TimeSpan.Zero));

        var result = await handler.HandleAsync(cmd);

        result.IsSuccess.Should().BeTrue();
        auditRepository.AddedEvents.Should().ContainSingle();

        var auditEvent = auditRepository.AddedEvents.Single();
        auditEvent.EventType.Should().Be(AuditEventTypes.CommentAdded);
        auditEvent.Actor.Should().Be("Mr. Wonder");

        using var payload = JsonDocument.Parse(auditEvent.Payload);
        payload.RootElement.GetProperty("author").GetString().Should().Be("Mr. Wonder");
        payload.RootElement.GetProperty("content").GetString().Should().Be("Readable history comment.");
        payload.RootElement.TryGetProperty("commentId", out _).Should().BeFalse();
        payload.RootElement.TryGetProperty("contentLength", out _).Should().BeFalse();
    }
    
    // -----------------------------------------------------------------------
    // Domain violations
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ContentTooLong_ReturnsValidationFailure()
    {
        var ticket = CreateTicket();
        var handler = CreateHandler(ticket, validator: new AddCommentValidator());
        var tooLongContent = new string('*', Comment.MaxContentLength + 1);

        var cmd = new AddCommentCommand(
            ticket.Id,
            Content: tooLongContent,
            Author: null,
            new DateTimeOffset());

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("add_comment.validation_failed");
        result.Error.FieldErrors.Should().ContainKey("content");
    }

    [Fact]
    public async Task HandleAsync_AuthorTooLong_ReturnsValidationFailure()
    {
        var ticket = CreateTicket();
        var handler = CreateHandler(ticket, validator: new AddCommentValidator());
        var tooLongAuthor = new string('*', Comment.MaxAuthorLength + 1);

        var cmd = new AddCommentCommand(
            ticket.Id,
            Content: "Love Connection.",
            Author: tooLongAuthor,
            new DateTimeOffset());

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("add_comment.validation_failed");
        result.Error.FieldErrors.Should().ContainKey("author");
    }

    [Fact]
    public async Task HandleAsync_EmptyContent_ReturnsValidationFailure()
    {
        var ticket = CreateTicket();
        var handler = CreateHandler(ticket, validator: new AddCommentValidator());
        var cmd = new AddCommentCommand(
            ticket.Id,
            Content: string.Empty,
            Author: null,
            new DateTimeOffset());

        var result = await handler.HandleAsync(cmd);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("add_comment.validation_failed");
        result.Error.FieldErrors.Should().ContainKey("content");
    }
    
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static AddCommentHandler CreateHandler(
        Ticket? existingTicket,
        FakeUnitOfWork? uow = null,
        FakeAuditEventRepository? auditRepository = null,
        ICommandValidator<AddCommentCommand>? validator = null)
    {
        var repo = new FakeTicketRepository(existingTicket);
        return new AddCommentHandler(
            repo,
            auditRepository ?? new FakeAuditEventRepository(),
            uow ?? new FakeUnitOfWork(),
            validator ?? new FakeValidator(),
            new FakeClock());
    }
    
    private static Ticket CreateTicket() => 
        new (TicketId.New(), "I love this feature", "Finally, I can add my two cents everywhere.", TicketPriority.Medium, DateTimeOffset.UtcNow);
    
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
        public List<AuditEvent> AddedEvents { get; } = [];

        public Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
        {
            AddedEvents.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEvent>> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
    
    private sealed class FakeValidator : ICommandValidator<AddCommentCommand>
    {
        public FieldValidationResult Validate(AddCommentCommand command) => FieldValidationResult.Ok;
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
