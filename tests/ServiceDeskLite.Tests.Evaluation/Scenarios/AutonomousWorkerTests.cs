using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Tests.Evaluation.Harness;

namespace ServiceDeskLite.Tests.Evaluation.Scenarios;

/// <summary>
/// The autonomous worker, judged by what it leaves behind on a ticket. What it may do unattended is
/// the guardrail; what it does with that authority is the point of the feature.
/// </summary>
/// <remarks>
/// The whole stack runs: the scan's scope, the shared agent loop, the sandbox, the tools, the
/// command handlers, the audit trail. Only the model's decisions are scripted — the suite evaluates
/// the harness around the model, never the model's judgment.
/// </remarks>
public sealed class AutonomousWorkerTests
{
    [Fact]
    public async Task It_asks_for_missing_information_and_parks_the_ticket()
    {
        using var host = new EvaluationHost();
        var client = host.CreateClient();

        var ticketId = await client.CreateTicketAsync("VPN broken", "It does not work.");

        host.Model
            .Then(ScriptedTurn.Create().Calls("add_comment", new
            {
                ticketId,
                content = "Which VPN client version are you on, and what is the exact error?",
            }))
            // New -> Triaged -> Waiting: the workflow allows no shortcut, and the prompt says so.
            .Then(ScriptedTurn.Create().Calls("change_ticket_status", new { ticketId, status = "Triaged" }))
            .Then(ScriptedTurn.Create().Calls("change_ticket_status", new { ticketId, status = "Waiting" }))
            .Then(ScriptedTurn.Create().Says("Asked the reporter for the missing details."));

        var outcome = await host.ReviewTicketAsync(ticketId);

        outcome.Referrals.Should().Be(0, "everything it did was within its policy");

        var comments = await client.CommentsAsync(ticketId);
        comments.Should().ContainSingle()
            .Which.GetProperty("content").GetString().Should().Contain("version");

        (await client.StatusOfAsync(ticketId)).Should().Be("Waiting",
            "a parked ticket must not sit in the queue as if it were actionable");
    }

    [Fact]
    public async Task Closing_a_ticket_is_refused_and_becomes_a_proposal_instead()
    {
        using var host = new EvaluationHost();
        var client = host.CreateClient();

        var ticketId = await client.CreateTicketAsync("Password reset", "Done, thanks!");

        host.Model
            // The model tries to finish the job on its own.
            .Then(ScriptedTurn.Create().Calls("change_ticket_status", new { ticketId, status = "Closed" }))
            // Reads the refusal, and does what it was told to do instead.
            .Then(ScriptedTurn.Create().Calls("add_comment", new
            {
                ticketId,
                content = "This looks resolved. I propose closing it. Closing needs your approval.",
            }))
            .Then(ScriptedTurn.Create().Says("Proposed closing the ticket."));

        var outcome = await host.ReviewTicketAsync(ticketId);

        outcome.Referrals.Should().Be(1, "the close was held back for a human");

        (await client.StatusOfAsync(ticketId)).Should().NotBe("Closed",
            "an unattended process must not finish somebody's ticket");

        var comments = await client.CommentsAsync(ticketId);
        comments.Should().ContainSingle()
            .Which.GetProperty("content").GetString().Should().Contain("approval");
    }

    [Fact]
    public async Task The_refusal_tells_the_model_to_comment_and_not_to_retry()
    {
        using var host = new EvaluationHost();
        var client = host.CreateClient();

        var ticketId = await client.CreateTicketAsync("Laptop dead", "Won't boot.");

        host.Model
            .Then(ScriptedTurn.Create().Calls("create_ticket", new
            {
                title = "Order replacement laptop",
                description = "Follow-up.",
                priority = "High",
            }))
            .Then(ScriptedTurn.Create().Says("I cannot open that ticket myself."));

        var before = await client.CountTicketsAsync();
        await host.ReviewTicketAsync(ticketId);

        // The guard's reason is the only thing the model learns about the rule. It has to name the
        // way out, or the model retries until its tool budget is gone.
        var refusal = host.Model.ToolResultsFor("toolu_1").Should().ContainSingle().Subject;
        refusal.GetProperty("is_error").GetBoolean().Should().BeTrue();

        var reason = refusal.GetProperty("content").GetString()!;
        reason.Should().Contain("add_comment");
        reason.Should().Contain("Do not retry");

        (await client.CountTicketsAsync()).Should().Be(before, "no ticket may be opened unattended");
    }

    [Fact]
    public async Task Everything_it_does_is_audited_as_the_worker_not_as_the_assistant()
    {
        using var host = new EvaluationHost();
        var client = host.CreateClient();

        var ticketId = await client.CreateTicketAsync("Printer jam", "Paper stuck on level 3.");

        host.Model
            .Then(ScriptedTurn.Create().Calls("change_ticket_status", new { ticketId, status = "Triaged" }))
            .Then(ScriptedTurn.Create().Says("Triaged."));

        await host.ReviewTicketAsync(ticketId);

        var actors = (await client.AuditEventsAsync(ticketId))
            .Select(e => e.GetProperty("actor").GetString())
            .ToList();

        actors.Should().Contain(AuditActors.AiWorker,
            "reading the trail, a background decision must be distinguishable from one a user asked for");
        actors.Should().NotContain(AuditActors.AiAssistant);
    }

    [Fact]
    public async Task It_grounds_a_proposal_in_the_knowledge_base_before_posting_it()
    {
        using var host = new EvaluationHost();
        host.KnowledgeBase.Returns(
            ScriptedKnowledgeBase.Passage("Clearing a paper jam", "Open tray 3 and remove the sheet."));

        var client = host.CreateClient();
        var ticketId = await client.CreateTicketAsync("Printer jam", "Paper stuck, tray 3, error E3.");

        host.Model
            .Then(ScriptedTurn.Create().Calls("search_knowledge_base", new { query = "paper jam tray 3" }))
            .Then(ScriptedTurn.Create().Calls("check_grounding", new
            {
                answer = "Open tray 3 and remove the sheet.",
            }))
            .Then(ScriptedTurn.Create().Calls("add_comment", new
            {
                ticketId,
                content = "Try this: open tray 3 and remove the sheet.",
            }))
            .Then(ScriptedTurn.Create().Says("Posted a grounded proposal."));

        var outcome = await host.ReviewTicketAsync(ticketId);

        outcome.Referrals.Should().Be(0);
        host.KnowledgeBase.LastQuery.Should().NotBeNull("the worker must look before it answers");

        var comments = await client.CommentsAsync(ticketId);
        comments.Should().ContainSingle()
            .Which.GetProperty("content").GetString().Should().Contain("tray 3");
    }

    [Fact]
    public async Task A_ticket_that_vanished_between_the_scan_and_the_review_is_not_an_error()
    {
        using var host = new EvaluationHost();

        var outcome = await host.ReviewTicketAsync(Guid.NewGuid());

        outcome.EndedEarly.Should().BeTrue();
        outcome.Actions.Should().Be(0);
        host.Model.RequestCount.Should().Be(0, "there was nothing to reason about, so nothing was billed");
    }

    [Fact]
    public async Task An_upstream_failure_ends_the_review_without_taking_the_worker_down()
    {
        using var host = new EvaluationHost();
        var client = host.CreateClient();

        var ticketId = await client.CreateTicketAsync("Disk full", "No space left on /var.");
        host.Model.FailsWith(System.Net.HttpStatusCode.ServiceUnavailable);

        var outcome = await host.ReviewTicketAsync(ticketId);

        outcome.EndedEarly.Should().BeTrue();
        outcome.Actions.Should().Be(0);

        // The next scan is a fresh attempt; the ticket is untouched and still eligible.
        (await client.StatusOfAsync(ticketId)).Should().Be("New");
    }

    [Fact]
    public async Task The_review_sees_the_comments_it_already_left_so_it_does_not_ask_twice()
    {
        using var host = new EvaluationHost();
        var client = host.CreateClient();

        var ticketId = await client.CreateTicketAsync("Mail sync", "Outlook is stuck.");

        host.Model
            .Then(ScriptedTurn.Create().Calls("add_comment", new { ticketId, content = "Which Outlook version?" }))
            .Then(ScriptedTurn.Create().Says("Asked."));
        await host.ReviewTicketAsync(ticketId);

        host.Model.Then(ScriptedTurn.Create().Says("Already asked; nothing new."));
        await host.ReviewTicketAsync(ticketId);

        var secondPrompt = host.Model.Requests[^1].GetRawText();
        secondPrompt.Should().Contain("Which Outlook version?",
            "without its own comments in the prompt, the worker asks the same question every scan");
    }
}
