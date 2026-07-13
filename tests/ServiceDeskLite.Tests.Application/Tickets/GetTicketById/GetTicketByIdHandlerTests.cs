using ServiceDeskLite.Tests.Application.Fakes;
﻿using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Application.Tickets.GetTicketById;

public class GetTicketByIdHandlerTests
{
    [Fact]
    public async Task Returns_not_found_when_ticket_missing()
    {
        var repo =  new FakeTicketRepository(null);
        var handler = new GetTicketByIdHandler(repo, new EmptyAgentRepository(), new FakeAuditEventRepository(), new FakeClock());
        
        var result = await handler.HandleAsync(new GetTicketByIdQuery(TicketId.New()));
        
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("ticket.not_found");
    }

    [Fact]
    public async Task Returns_ticket_details_when_ticket_exists()
    {
        var id = TicketId.New();
        var ticket = new Ticket(
            id,
            "Title",
            "Desc",
            TicketPriority.Medium,
            DateTimeOffset.UtcNow);
        
        var repo =  new FakeTicketRepository(ticket);
        var handler = new GetTicketByIdHandler(repo, new EmptyAgentRepository(), new FakeAuditEventRepository(), new FakeClock());
        
        var result = await handler.HandleAsync(new GetTicketByIdQuery(id));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(id);
        result.Value.Title.Should().Be("Title");
        result.Value.AllowedTransitions.Should().Equal(TicketStatus.Triaged);
    }

    [Fact]
    public async Task Suggested_steps_are_tagged_with_the_action_they_route_to()
    {
        var id = TicketId.New();
        // A fresh ticket is New, unassigned, and has no due date.
        var ticket = new Ticket(id, "Title", "Desc", TicketPriority.Medium, DateTimeOffset.UtcNow);

        var repo = new FakeTicketRepository(ticket);
        var handler = new GetTicketByIdHandler(repo, new EmptyAgentRepository(), new FakeAuditEventRepository(), new FakeClock());

        var result = await handler.HandleAsync(new GetTicketByIdQuery(id));

        var steps = result.Value!.SuggestedNextSteps;

        // Unassigned tickets recommend assigning first, and it routes to the Assign action.
        steps[0].Action.Should().Be(SuggestedActionKind.Assign);
        // The workflow-move step carries the concrete target status.
        steps.Should().ContainSingle(s => s.Action == SuggestedActionKind.ChangeStatus)
            .Which.TargetStatus.Should().Be(TicketStatus.Triaged);
        // Adding a due date is a details edit, not a routed action - it stays advisory text.
        steps.Should().Contain(s => s.Text.Contains("due date") && s.Action == SuggestedActionKind.None);
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class FakeAuditEventRepository : IAuditEventRepository
    {
        public Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<AuditEvent>> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AuditEvent>>([]);
    }

    private sealed class FakeTicketRepository : ITicketRepository
    {
        private readonly Ticket? _ticket;
        
        public FakeTicketRepository(Ticket? ticket) => _ticket = ticket;
        
        public Task AddAsync(Ticket ticket, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default)
            => Task.FromResult(_ticket is not null && _ticket.Id.Equals(id) ? _ticket : null);

        public Task<bool> ExistsAsync(TicketId id, CancellationToken ct)
            => Task.FromResult(false);

        public Task<PagedResult<TicketListItemDto>> SearchAsync(
            TicketSearchCriteria criteria,
            Paging paging,
            SortSpec sort,
            CancellationToken ct = default)
            => Task.FromResult(new PagedResult<TicketListItemDto>([], 0, paging));
    }
}
