using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Domain.Tickets;

public sealed class TicketTests
{
    [Fact]
    public void NewTicket_StartsWithStatus_New()
    {
        // Arrange
        var ticket = CreateTicket();

        // Act
        var status = ticket.Status;

        // Assert
        Assert.Equal(TicketStatus.New, status);
    }

    [Fact]
    public void ChangeStatus_New_To_Triaged_UpdatesStatus()
    {
        // Arrange
        var ticket = CreateTicket();

        // Act
        ticket.ChangeStatus(TicketStatus.Triaged);

        // Assert
        Assert.Equal(TicketStatus.Triaged, ticket.Status);
    }

    [Fact]
    public void ChangeStatus_InvalidTransition_ThrowsDomainException_WithExpectedErrorCode()
    {
        // Arrange
        var ticket = CreateTicket();

        // Act
        var ex = Assert.Throws<DomainException>(() => ticket.ChangeStatus(TicketStatus.Closed));

        // Assert
        Assert.Equal("ticket.invalid_transition", ex.Error.Code);
    }

    [Fact]
    public void ChangeStatus_ReopenFromResolved_To_InProgress_IsAllowed()
    {
        // Arrange
        var ticket = CreateTicket();

        // Act
        ticket.ChangeStatus(TicketStatus.Triaged);
        ticket.ChangeStatus(TicketStatus.InProgress);
        ticket.ChangeStatus(TicketStatus.Resolved);

        // Assert precondition
        Assert.Equal(TicketStatus.Resolved, ticket.Status);

        // Act (reopen)
        ticket.ChangeStatus(TicketStatus.InProgress);

        // Assert
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
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

        Assert.Equal("domain.not_empty", ex.Error.Code);
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

        Assert.Equal("domain.max_length", ex.Error.Code);
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
