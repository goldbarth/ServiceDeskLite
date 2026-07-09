using FluentAssertions;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Api.Assistant.Sandbox;
using ServiceDeskLite.Api.Worker;
using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Assistant.Sandbox;

/// <summary>
/// The line between what an unattended agent may do and what it must ask about. Everything on the
/// wrong side of it reaches a person as a comment instead of reaching the domain as a write.
/// </summary>
public sealed class HumanReviewGuardTests
{
    /// <summary>
    /// Built the way the container builds it: the collections bind empty and
    /// <see cref="AutonomousWorkerOptions.ApplyDefaults"/> fills them afterwards. A guard handed a raw
    /// instance would see an empty policy and refuse everything, which is not what a deployment gets.
    /// </summary>
    private static HumanReviewGuard Guard(AutonomousWorkerOptions? policy = null)
    {
        policy ??= new AutonomousWorkerOptions();
        policy.ApplyDefaults();

        return new HumanReviewGuard(Options.Create(policy));
    }

    [Theory]
    [InlineData(CreateTicketTool.Name)]
    [InlineData(ChangeTicketStatusTool.Name)]
    [InlineData(AssignTicketTool.Name)]
    [InlineData(UpdateTicketTool.Name)]
    [InlineData(RouteTicketTool.Name)]
    public void An_interactive_agent_is_never_constrained(string tool)
    {
        // The user asked for it and watches it happen. That is the review.
        var result = Guard().Check(SandboxTestContext.Context(tool, mode: AgentMode.Interactive));

        result.IsAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(SearchTicketsTool.Name)]
    [InlineData(SearchKnowledgeBaseTool.Name)]
    [InlineData(FindSimilarTicketsTool.Name)]
    [InlineData(RecallMemoryTool.Name)]
    [InlineData(CheckGroundingTool.Name)]
    public void An_unattended_agent_may_always_read(string tool)
    {
        var result = Guard().Check(SandboxTestContext.Context(tool, mode: AgentMode.Autonomous));

        result.IsAllowed.Should().BeTrue("reading a ticket harms nobody and needs nobody's approval");
    }

    [Fact]
    public void An_unattended_agent_may_comment_because_that_is_how_it_reaches_a_person()
    {
        var result = Guard().Check(SandboxTestContext.Context(AddCommentTool.Name, mode: AgentMode.Autonomous));

        result.IsAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(CreateTicketTool.Name)]
    [InlineData(AssignTicketTool.Name)]
    [InlineData(UpdateTicketTool.Name)]
    [InlineData(RouteTicketTool.Name)]
    [InlineData(RememberTool.Name)]
    public void An_unattended_write_is_refused_and_told_to_ask_instead(string tool)
    {
        var result = Guard().Check(SandboxTestContext.Context(tool, mode: AgentMode.Autonomous));

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain(AddCommentTool.Name,
            "a refusal that does not say what to do instead teaches the model nothing");
        result.Reason.Should().Contain("Do not retry",
            "otherwise the model burns its tool budget rediscovering the same rule");
    }

    [Theory]
    [InlineData("Waiting")] // "I asked, and I am waiting for an answer."
    [InlineData("Triaged")] // The only way out of New, and it classifies rather than finishes.
    public void Sorting_a_ticket_without_finishing_it_needs_no_approval(string status)
    {
        var result = Guard().Check(SandboxTestContext.Context(
            ChangeTicketStatusTool.Name,
            input: $$"""{"ticketId":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","status":"{{status}}"}""",
            mode: AgentMode.Autonomous));

        result.IsAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData("Closed")]
    [InlineData("Resolved")]
    [InlineData("InProgress")]
    public void Closing_or_resolving_a_ticket_unattended_is_refused(string status)
    {
        var result = Guard().Check(SandboxTestContext.Context(
            ChangeTicketStatusTool.Name,
            input: $$"""{"ticketId":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","status":"{{status}}"}""",
            mode: AgentMode.Autonomous));

        result.IsAllowed.Should().BeFalse("finishing someone's ticket is a decision a person makes");
    }

    [Fact]
    public void A_status_change_with_unreadable_input_is_not_an_approved_transition()
    {
        // The tool will reject this in a moment with a better message. The guard must not read
        // "I could not find a status" as "the status was Waiting".
        var result = Guard().Check(SandboxTestContext.Context(
            ChangeTicketStatusTool.Name, input: """{"ticketId":"not-a-uuid"}""", mode: AgentMode.Autonomous));

        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void The_policy_decides_which_writes_are_autonomous_not_the_guard()
    {
        var policy = new AutonomousWorkerOptions
        {
            AutonomousWrites = [AddCommentTool.Name, AssignTicketTool.Name],
            AutonomousStatusTransitions = [TicketStatus.Waiting, TicketStatus.Resolved],
        };

        Guard(policy).Check(SandboxTestContext.Context(AssignTicketTool.Name, mode: AgentMode.Autonomous))
            .IsAllowed.Should().BeTrue();

        Guard(policy).Check(SandboxTestContext.Context(
                ChangeTicketStatusTool.Name,
                input: """{"ticketId":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","status":"Resolved"}""",
                mode: AgentMode.Autonomous))
            .IsAllowed.Should().BeTrue();

        // Still not granted by that policy.
        Guard(policy).Check(SandboxTestContext.Context(CreateTicketTool.Name, mode: AgentMode.Autonomous))
            .IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void The_guard_spends_nothing_so_a_refusal_costs_no_budget()
    {
        var turn = new ToolTurnState();

        var context = SandboxTestContext.Context(CreateTicketTool.Name, turn: turn, mode: AgentMode.Autonomous);
        IToolGuard guard = Guard();
        guard.Check(context);
        guard.Commit(context);

        turn.Writes.Should().Be(0);
    }
}
