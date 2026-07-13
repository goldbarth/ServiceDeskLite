using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Tests.Evaluation.Harness;

namespace ServiceDeskLite.Tests.Evaluation.Scenarios;

/// <summary>
/// The grounding self-check is enforced, not requested (ADR-0039). Once a knowledge-base search
/// has recorded passages and no <c>check_grounding</c> has run, the loop constrains the next model
/// turn to <c>check_grounding</c> via <c>tool_choice</c>, so the verdict is computed before the
/// answer streams. These scenarios read the <c>tool_choice</c> the loop actually sent upstream -
/// the scripted handler ignores it, so what is asserted is our constraint, not the model's reply.
/// </summary>
public sealed class GroundingEnforcementTests
{
    private const string ResetPassage =
        "To reset a locked account, open the admin console and choose Reset Password. "
        + "The user receives a one-time link valid for 30 minutes.";

    private static (string Type, string? Name) ToolChoiceOf(JsonElement request)
    {
        if (!request.TryGetProperty("tool_choice", out var choice))
            return ("(absent)", null);

        var type = choice.GetProperty("type").GetString();
        var name = choice.TryGetProperty("name", out var n) ? n.GetString() : null;
        return (type ?? "(null)", name);
    }

    [Fact]
    public async Task After_a_retrieval_the_next_turn_is_forced_to_check_grounding()
    {
        using var host = new EvaluationHost();
        host.KnowledgeBase.Returns(ScriptedKnowledgeBase.Passage("Resetting a locked account", ResetPassage));

        // The model searches, then would answer straight away. The loop must interpose the check.
        host.Model
            .Then(ScriptedTurn.Create().Calls("search_knowledge_base", new { query = "reset locked account" }))
            .Then(ScriptedTurn.Create().Calls("check_grounding", new
            {
                answer = "Open the admin console and choose Reset Password.",
            }))
            .Then(ScriptedTurn.Create().Says("Open the admin console and choose Reset Password."));

        await host.CreateClient().ChatAsync("How do I reset a locked account?");

        host.Model.RequestCount.Should().Be(3);
        ToolChoiceOf(host.Model.Requests[0]).Should().Be(("auto", (string?)null),
            "no passages have been retrieved on the first turn, so nothing is forced");
        ToolChoiceOf(host.Model.Requests[1]).Should().Be(("tool", "check_grounding"),
            "passages exist and no check has run, so the grounding check is forced");
        ToolChoiceOf(host.Model.Requests[2]).Should().Be(("auto", (string?)null),
            "the check has run; the model is free to give its answer");
    }

    [Fact]
    public async Task A_model_that_checks_itself_in_the_same_turn_is_never_forced()
    {
        using var host = new EvaluationHost();
        host.KnowledgeBase.Returns(ScriptedKnowledgeBase.Passage("Resetting a locked account", ResetPassage));

        // Search and check land in one assistant turn; the check runs before the next request is built.
        host.Model
            .Then(ScriptedTurn.Create()
                .Calls("search_knowledge_base", new { query = "reset locked account" })
                .Calls("check_grounding", new { answer = "Open the admin console and choose Reset Password." }))
            .Then(ScriptedTurn.Create().Says("Open the admin console and choose Reset Password."));

        await host.CreateClient().ChatAsync("How do I reset a locked account?");

        host.Model.RequestCount.Should().Be(2);
        ToolChoiceOf(host.Model.Requests[1]).Should().Be(("auto", (string?)null),
            "the model already grounded its own draft, so there is nothing left to force");
    }

    [Fact]
    public async Task Without_any_retrieval_the_check_is_never_forced()
    {
        using var host = new EvaluationHost();

        // No search_knowledge_base, so IRagRetrievalContext stays empty and grounding does not apply.
        host.Model
            .Then(ScriptedTurn.Create().Calls("create_ticket", new
            {
                title = "Printer offline", description = "Third floor printer is dead.", priority = "High",
            }))
            .Then(ScriptedTurn.Create().Says("Filed it."));

        await host.CreateClient().ChatAsync("The third floor printer is dead.");

        host.Model.Requests.Select(ToolChoiceOf).Should().OnlyContain(c => c.Type == "auto",
            "grounding is only forced once knowledge-base passages have been retrieved");
    }

    [Fact]
    public async Task The_check_is_forced_once_a_later_search_does_not_force_it_again()
    {
        using var host = new EvaluationHost();
        host.KnowledgeBase.Returns(ScriptedKnowledgeBase.Passage("Resetting a locked account", ResetPassage));

        host.Model
            .Then(ScriptedTurn.Create().Calls("search_knowledge_base", new { query = "reset locked account" }))
            .Then(ScriptedTurn.Create().Calls("check_grounding", new { answer = "Reset it in the admin console." }))
            .Then(ScriptedTurn.Create().Calls("search_knowledge_base", new { query = "link expiry" }))
            .Then(ScriptedTurn.Create().Says("The reset link is valid for 30 minutes."));

        await host.CreateClient().ChatAsync("How do I reset a locked account, and how long is the link valid?");

        ToolChoiceOf(host.Model.Requests[1]).Should().Be(("tool", "check_grounding"),
            "the first turn after retrieval is forced");
        ToolChoiceOf(host.Model.Requests[3]).Should().Be(("auto", (string?)null),
            "force-once: after one check the model is free, even when it retrieves again");
    }
}
