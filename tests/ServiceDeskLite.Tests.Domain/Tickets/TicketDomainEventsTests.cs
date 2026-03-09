using FluentAssertions;

using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Domain.Tickets.Events;

namespace ServiceDeskLite.Tests.Domain.Tickets;

public sealed class TicketDomainEventsTests
{
    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_RaisesExactlyOneTicketCreatedDomainEvent()
    {
        var ticket = CreateTicket();

        ticket.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TicketCreatedDomainEvent>();
    }

    [Fact]
    public void Constructor_TicketCreatedEvent_CarriesCorrectData()
    {
        var ticket = CreateTicket(title: "Login broken", priority: TicketPriority.High);

        var e = ticket.DomainEvents.OfType<TicketCreatedDomainEvent>().Single();
        e.TicketId.Should().Be(ticket.Id);
        e.Title.Should().Be("Login broken");
        e.Priority.Should().Be(TicketPriority.High);
    }

    // -----------------------------------------------------------------------
    // ChangeStatus
    // -----------------------------------------------------------------------

    [Fact]
    public void ChangeStatus_ValidTransition_RaisesStatusChangedDomainEvent()
    {
        var ticket = CreateTicket();
        ticket.ClearDomainEvents();

        ticket.ChangeStatus(TicketStatus.Triaged);

        ticket.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<StatusChangedDomainEvent>();
    }

    [Fact]
    public void ChangeStatus_StatusChangedEvent_CarriesFromAndToStatus()
    {
        var ticket = CreateTicket();
        ticket.ClearDomainEvents();

        ticket.ChangeStatus(TicketStatus.Triaged);

        var e = ticket.DomainEvents.OfType<StatusChangedDomainEvent>().Single();
        e.FromStatus.Should().Be(TicketStatus.New);
        e.ToStatus.Should().Be(TicketStatus.Triaged);
    }

    [Fact]
    public void ChangeStatus_InvalidTransition_DoesNotRaiseDomainEvent()
    {
        var ticket = CreateTicket();
        ticket.ClearDomainEvents();

        var act = () => ticket.ChangeStatus(TicketStatus.Closed);

        act.Should().Throw<DomainException>();
        ticket.DomainEvents.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Assign
    // -----------------------------------------------------------------------

    [Fact]
    public void Assign_RaisesAssigneeChangedDomainEvent()
    {
        var ticket = CreateTicket();
        ticket.ClearDomainEvents();

        ticket.Assign(new Assignee("Alice"));

        ticket.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AssigneeChangedDomainEvent>();
    }

    [Fact]
    public void Assign_AssigneeChangedEvent_CarriesPreviousAndNewAssignee()
    {
        var ticket = CreateTicket();
        ticket.Assign(new Assignee("Alice"));
        ticket.ClearDomainEvents();

        ticket.Assign(new Assignee("Bob"));

        var e = ticket.DomainEvents.OfType<AssigneeChangedDomainEvent>().Single();
        e.PreviousAssignee.Should().Be("Alice");
        e.NewAssignee.Should().Be("Bob");
    }

    [Fact]
    public void Assign_ToNull_AssigneeChangedEvent_HasNullNewAssignee()
    {
        var ticket = CreateTicket();
        ticket.Assign(new Assignee("Alice"));
        ticket.ClearDomainEvents();

        ticket.Assign(null);

        var e = ticket.DomainEvents.OfType<AssigneeChangedDomainEvent>().Single();
        e.PreviousAssignee.Should().Be("Alice");
        e.NewAssignee.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // AddComment
    // -----------------------------------------------------------------------

    [Fact]
    public void AddComment_RaisesCommentAddedDomainEvent()
    {
        var ticket = CreateTicket();
        ticket.ClearDomainEvents();

        ticket.AddComment("Hello!", DateTimeOffset.UtcNow);

        ticket.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CommentAddedDomainEvent>();
    }

    [Fact]
    public void AddComment_CommentAddedEvent_CarriesCorrectData()
    {
        var ticket = CreateTicket();
        ticket.ClearDomainEvents();

        const string content = "Hello world!";
        ticket.AddComment(content, DateTimeOffset.UtcNow, "Bob");

        var e = ticket.DomainEvents.OfType<CommentAddedDomainEvent>().Single();
        e.TicketId.Should().Be(ticket.Id);
        e.Author.Should().Be("Bob");
        e.Content.Should().Be(content);
    }

    // -----------------------------------------------------------------------
    // ClearDomainEvents
    // -----------------------------------------------------------------------

    [Fact]
    public void ClearDomainEvents_EmptiesTheList()
    {
        var ticket = CreateTicket();
        ticket.DomainEvents.Should().NotBeEmpty();

        ticket.ClearDomainEvents();

        ticket.DomainEvents.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Ticket CreateTicket(
        string title = "Test ticket",
        TicketPriority priority = TicketPriority.Medium)
        => new(TicketId.New(), title, "Description.", priority, DateTimeOffset.UtcNow);
}
