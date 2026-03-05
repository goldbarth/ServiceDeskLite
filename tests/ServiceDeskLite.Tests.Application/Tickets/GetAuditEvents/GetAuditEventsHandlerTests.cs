using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.GetAuditEvents;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Application.Tickets.GetAuditEvents;

public sealed class GetAuditEventsHandlerTests
{
    // -----------------------------------------------------------------------
    // Guard: input validation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_NullQuery_ReturnsValidationFailure()
    {
        var handler = CreateHandler(ticketExists: false);

        var result = await handler.HandleAsync(null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("get_audit_events.query.null");
    }

    // -----------------------------------------------------------------------
    // Not Found
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_TicketNotFound_ReturnsNotFound()
    {
        var handler = CreateHandler(ticketExists: false);

        var result = await handler.HandleAsync(new GetAuditEventsQuery(TicketId.New()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("ticket.not_found");
    }

    // -----------------------------------------------------------------------
    // Success paths
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_TicketExists_NoEvents_ReturnsEmptyList()
    {
        var handler = CreateHandler(ticketExists: true, auditEvents: []);

        var result = await handler.HandleAsync(new GetAuditEventsQuery(TicketId.New()));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_TicketExists_ReturnsMappedDtos()
    {
        var ticketId = TicketId.New();
        var occurredAt = DateTimeOffset.UtcNow;

        var auditEvent = new AuditEvent(
            AuditEventId.New(),
            ticketId,
            AuditEventTypes.TicketCreated,
            actor: "alice",
            occurredAt,
            "{\"title\":\"Test\"}");

        var handler = CreateHandler(ticketExists: true, auditEvents: [auditEvent]);

        var result = await handler.HandleAsync(new GetAuditEventsQuery(ticketId));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);

        var dto = result.Value![0];
        dto.Id.Should().Be(auditEvent.Id.Value);
        dto.EventType.Should().Be(AuditEventTypes.TicketCreated);
        dto.Actor.Should().Be("alice");
        dto.OccurredAt.Should().Be(occurredAt);
        dto.Payload.Should().Be("{\"title\":\"Test\"}");
    }

    [Fact]
    public async Task HandleAsync_TicketExists_ReturnsAllEvents()
    {
        var ticketId = TicketId.New();
        var events = new[]
        {
            BuildAuditEvent(ticketId, AuditEventTypes.TicketCreated),
            BuildAuditEvent(ticketId, AuditEventTypes.StatusChanged),
            BuildAuditEvent(ticketId, AuditEventTypes.AssigneeChanged)
        };

        var handler = CreateHandler(ticketExists: true, auditEvents: events);

        var result = await handler.HandleAsync(new GetAuditEventsQuery(ticketId));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(3);
        result.Value!.Select(d => d.EventType).Should().BeEquivalentTo(
            AuditEventTypes.TicketCreated,
            AuditEventTypes.StatusChanged,
            AuditEventTypes.AssigneeChanged);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static GetAuditEventsHandler CreateHandler(
        bool ticketExists,
        IReadOnlyList<AuditEvent>? auditEvents = null)
    {
        var ticketRepo = new FakeTicketRepository(ticketExists);
        var auditRepo = new FakeAuditEventRepository(auditEvents ?? []);
        return new GetAuditEventsHandler(ticketRepo, auditRepo);
    }

    private static AuditEvent BuildAuditEvent(TicketId ticketId, string eventType)
        => new(AuditEventId.New(), ticketId, eventType, actor: null, DateTimeOffset.UtcNow, "{}");

    private sealed class FakeTicketRepository(bool exists) : ITicketRepository
    {
        public Task AddAsync(Ticket ticket, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default)
            => Task.FromResult<Ticket?>(null);

        public Task<bool> ExistsAsync(TicketId id, CancellationToken ct)
            => Task.FromResult(exists);

        public Task<PagedResult<Ticket>> SearchAsync(
            TicketSearchCriteria criteria, Paging paging, SortSpec sort,
            CancellationToken ct = default)
            => Task.FromResult(new PagedResult<Ticket>([], 0, paging));
    }

    private sealed class FakeAuditEventRepository(IReadOnlyList<AuditEvent> events) : IAuditEventRepository
    {
        public Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<AuditEvent>> GetByTicketIdAsync(
            TicketId ticketId, CancellationToken ct = default)
            => Task.FromResult(events);
    }
}
