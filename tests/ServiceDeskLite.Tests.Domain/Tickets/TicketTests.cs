using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;
using FluentAssertions;

namespace ServiceDeskLite.Tests.Domain.Tickets;

public sealed class TicketTests
{
    [Fact]
    public void NewTicket_StartsWithStatus_New()
    {
        var ticket = CreateTicket();
        ticket.Status.Should().Be(TicketStatus.New);
    }

    [Fact]
    public void ChangeStatus_New_To_Triaged_UpdatesStatus()
    {
        var  ticket = CreateTicket();
        ticket.ChangeStatus(TicketStatus.Triaged);
        ticket.Status.Should().Be(TicketStatus.Triaged);
    }

    [Fact]
    public void ChangeStatus_InvalidTransition_ThrowsDomainException_WithExpectedErrorCode()
    {
        var ticket = CreateTicket();
        var ex = Assert.Throws<DomainException>(() => ticket.ChangeStatus(TicketStatus.Closed));
        ex.Error.Code.Should().Be("domain.ticket.status.invalid_transition");
    }

    [Fact]
    public void ChangeStatus_ReopenFromResolved_To_InProgress_IsAllowed()
    {
        var ticket = CreateTicket();
        ticket.ChangeStatus(TicketStatus.Triaged);
        ticket.ChangeStatus(TicketStatus.InProgress);
        ticket.ChangeStatus(TicketStatus.Resolved);

        ticket.Status.Should().Be(TicketStatus.Resolved);

        ticket.ChangeStatus(TicketStatus.InProgress);

        ticket.Status.Should().Be(TicketStatus.InProgress);
    }
    
    [Fact]
    public void Ctor_EmptyTitle_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Ticket(
                TicketId.New(),
                title: "",
                description: "desc",
                priority: TicketPriority.Low,
                createdAt: DateTimeOffset.UtcNow));
        
        ex.Error.Code.Should().Be("domain.not_empty");
    }

    [Fact]
    public void Ctor_TooLongDescription_ThrowsDomainException()
    {
        var tooLong = new string('x', 2001);

        var ex = Assert.Throws<DomainException>(() =>
            new Ticket(
                TicketId.New(),
                title: "title",
                description: tooLong,
                priority: TicketPriority.Low,
                createdAt: DateTimeOffset.UtcNow));
        
        ex.Error.Code.Should().Be("domain.max_length");
    }
    
    private static Ticket CreateTicket(
        TicketStatus? initialStatus = null)
    {
        var ticket = new Ticket(
            id: TicketId.New(),
            title: "Cannot login",
            description: "User reports login fails with 401.",
            priority: TicketPriority.Medium,
            createdAt: DateTimeOffset.UtcNow,
            dueAt: null);

        // goldbarth: Tests with different start status (legitimate domain API):
        // if (initialStatus is not null) { ... }

        return ticket;
    }

}
