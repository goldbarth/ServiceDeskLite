using FluentAssertions;

using ServiceDeskLite.Tests.Evaluation.Harness;

namespace ServiceDeskLite.Tests.Evaluation.Scenarios;

/// <summary>
/// Retrieval and grounding, with the corpus fixed by the test so the passages an answer is
/// graded against are known. Covers the two failure modes that matter: citing a source that was
/// never retrieved, and claiming things the retrieved sources do not support.
/// </summary>
public sealed class RagGroundingTests
{
    private const string ResetPassage =
        "To reset a locked account, open the admin console and choose Reset Password. "
        + "The user receives a one-time link valid for 30 minutes.";

    [Fact]
    public async Task Retrieved_passages_are_surfaced_to_the_client_as_citations()
    {
        using var host = new EvaluationHost();
        host.KnowledgeBase.Returns(ScriptedKnowledgeBase.Passage("Resetting a locked account", ResetPassage));
        host.Model
            .Then(ScriptedTurn.Create().Calls("search_knowledge_base", new { query = "reset locked account" }))
            .Then(ScriptedTurn.Create().Says("Open the admin console and choose Reset Password."));

        var events = await host.CreateClient().ChatAsync("How do I reset a locked account?");

        var citation = events.OfType(AgentTranscript.CitationEvent).Should().ContainSingle().Subject;
        citation.ToolName.Should().Be("search_knowledge_base");
        citation.Data.GetProperty("citations").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("heading").GetString().Should().Be("Resetting a locked account");

        host.KnowledgeBase.LastQuery.Should().Be("reset locked account");
    }

    [Fact]
    public async Task An_unavailable_knowledge_base_cites_nothing_and_says_so()
    {
        // No passages seeded: the host behaves like a deployment with no Voyage key.
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Calls("search_knowledge_base", new { query = "reset locked account" }))
            .Then(ScriptedTurn.Create().Says("I could not consult internal sources."));

        var events = await host.CreateClient().ChatAsync("How do I reset a locked account?");

        events.OfType(AgentTranscript.CitationEvent).Should().BeEmpty(
            "an unavailable search must never produce a source");

        var toolResult = events.OfType(AgentTranscript.ToolResultEvent).Single();
        toolResult.IsError.Should().BeFalse("unavailability is a fact to report, not a failure");
        toolResult.Message.Should().Contain("not available");
    }

    [Fact]
    public async Task An_available_search_that_matches_nothing_is_distinct_from_no_search_at_all()
    {
        using var host = new EvaluationHost();
        host.KnowledgeBase.ReturnsNothing();
        host.Model
            .Then(ScriptedTurn.Create().Calls("search_knowledge_base", new { query = "quantum tunnelling" }))
            .Then(ScriptedTurn.Create().Says("Nothing relevant was found."));

        var events = await host.CreateClient().ChatAsync("Explain quantum tunnelling.");

        var toolResult = events.OfType(AgentTranscript.ToolResultEvent).Single();
        toolResult.Message.Should().Contain("No knowledge-base passages matched");
        events.OfType(AgentTranscript.CitationEvent).Should().BeEmpty();
    }

    [Fact]
    public async Task Grounding_scores_an_answer_against_the_passages_that_were_actually_retrieved()
    {
        using var host = new EvaluationHost();
        host.KnowledgeBase.Returns(ScriptedKnowledgeBase.Passage("Resetting a locked account", ResetPassage));
        host.Model
            .Then(ScriptedTurn.Create().Calls("search_knowledge_base", new { query = "reset locked account" }))
            .Then(ScriptedTurn.Create().Calls("check_grounding", new
            {
                answer = "Open the admin console and choose Reset Password. The one-time link is valid for 30 minutes.",
            }))
            .Then(ScriptedTurn.Create().Says("Open the admin console and choose Reset Password."));

        var events = await host.CreateClient().ChatAsync("How do I reset a locked account?");

        var grounding = events.OfType(AgentTranscript.ToolResultEvent)
            .Single(e => e.ToolName == "check_grounding");

        grounding.IsError.Should().BeFalse();
        grounding.Data.GetProperty("confidence").GetDouble().Should().BeGreaterThan(0.5,
            "the answer restates the retrieved passage");
    }

    [Fact]
    public async Task An_unsupported_answer_scores_low_so_the_agent_can_hedge()
    {
        using var host = new EvaluationHost();
        host.KnowledgeBase.Returns(ScriptedKnowledgeBase.Passage("Resetting a locked account", ResetPassage));
        host.Model
            .Then(ScriptedTurn.Create().Calls("search_knowledge_base", new { query = "reset locked account" }))
            .Then(ScriptedTurn.Create().Calls("check_grounding", new
            {
                answer = "Phone the night porter, who keeps the master key in a drawer beneath the fire alarm.",
            }))
            .Then(ScriptedTurn.Create().Says("I am not certain; the sources do not cover that."));

        var events = await host.CreateClient().ChatAsync("How do I reset a locked account?");

        var grounding = events.OfType(AgentTranscript.ToolResultEvent)
            .Single(e => e.ToolName == "check_grounding");

        grounding.Data.GetProperty("confidence").GetDouble().Should().BeLessThan(0.5,
            "nothing in the retrieved passage supports this answer");
    }

    [Fact]
    public async Task Grounding_without_a_prior_retrieval_has_nothing_to_check_against()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Calls("check_grounding", new { answer = "Reset it in the admin console." }))
            .Then(ScriptedTurn.Create().Says("Let me search first."));

        var events = await host.CreateClient().ChatAsync("How do I reset a locked account?");

        var grounding = events.OfType(AgentTranscript.ToolResultEvent).Single();
        grounding.Message.Should().Contain("search_knowledge_base",
            "the tool tells the model to retrieve before it grades itself");
    }
}
