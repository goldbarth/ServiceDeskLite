using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Domain.Tickets;

public sealed class TicketWorkflowTests
{
    [Fact]
    public void CanTransition_New_To_Triaged_ReturnsTrue()
    {
        // Arrange
        const TicketStatus from = TicketStatus.New;
        const TicketStatus to = TicketStatus.Triaged;

        // Act
        var canTransition = TicketWorkflow.CanTransition(from, to);

        // Assert
        Assert.True(canTransition);
    }
    
    [Fact]
    public void EnsureCanTransition_New_To_Triaged_DoesNotThrow()
    {
        // Arrange
        const TicketStatus from = TicketStatus.New;
        const TicketStatus to = TicketStatus.Triaged;

        // Act
        var exception = Record.Exception(() => TicketWorkflow.EnsureCanTransition(from, to));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void CanTransition_New_To_Resolved_ReturnsFalse()
    {
        // Arrange
        const TicketStatus from = TicketStatus.New;
        const TicketStatus to = TicketStatus.Resolved;

        // Act
        var canTransition = TicketWorkflow.CanTransition(from, to);

        // Assert
        Assert.False(canTransition);
    }

    [Fact]
    public void EnsureCanTransition_New_To_Resolved_ThrowsDomainException_WithExpectedErrorCode()
    {
        // Arrange
        const TicketStatus from = TicketStatus.New;
        const TicketStatus to = TicketStatus.Resolved;

        // Act
        var ex = Assert.Throws<DomainException>(() => TicketWorkflow.EnsureCanTransition(from, to));

        // Assert
        Assert.Equal("ticket.invalid_transition", ex.Error.Code);
    }

    [Fact]
    public void EnsureCanTransition_Resolved_To_InProgress_DoesNotThrow()
    {
        // Arrange
        const TicketStatus from = TicketStatus.Resolved;
        const TicketStatus to = TicketStatus.InProgress;

        // Act
        var exception = Record.Exception(() => TicketWorkflow.EnsureCanTransition(from, to));

        // Assert
        Assert.Null(exception);
    }
}
