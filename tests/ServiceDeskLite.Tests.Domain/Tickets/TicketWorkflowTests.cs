using FluentAssertions;

using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Domain.Tickets;

public sealed class TicketWorkflowTests
{
    [Fact]
    public void CanTransition_New_To_Triaged_ReturnsTrue()
    {
        const TicketStatus from = TicketStatus.New;
        const TicketStatus to = TicketStatus.Triaged;
        
        var canTransition = TicketWorkflow.CanTransition(from, to);

        canTransition.Should().BeTrue();
    }
    
    [Fact]
    public void EnsureCanTransition_New_To_Triaged_DoesNotThrow()
    {
        const TicketStatus from = TicketStatus.New;
        const TicketStatus to = TicketStatus.Triaged;

        var ex = Record.Exception(() => TicketWorkflow.EnsureCanTransition(from, to));
        
        ex.Should().BeNull();
    }

    [Fact]
    public void CanTransition_New_To_Resolved_ReturnsFalse()
    {
        const TicketStatus from = TicketStatus.New;
        const TicketStatus to = TicketStatus.Resolved;

        var canTransition = TicketWorkflow.CanTransition(from, to);

        canTransition.Should().BeFalse();
    }

    [Fact]
    public void EnsureCanTransition_New_To_Resolved_ThrowsDomainException_WithExpectedErrorCode()
    {
        const TicketStatus from = TicketStatus.New;
        const TicketStatus to = TicketStatus.Resolved;
        
        var ex = Assert.Throws<DomainException>(() => TicketWorkflow.EnsureCanTransition(from, to));
        
        ex.Error.Code.Should().Be("domain.ticket.status.invalid_transition");
    }

    [Fact]
    public void EnsureCanTransition_Resolved_To_InProgress_DoesNotThrow()
    {
        const TicketStatus from = TicketStatus.Resolved;
        const TicketStatus to = TicketStatus.InProgress;
        
        var ex = Record.Exception(() => TicketWorkflow.EnsureCanTransition(from, to));
        ex.Should().BeNull();
    }
}
