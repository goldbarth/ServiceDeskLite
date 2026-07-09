using System.Diagnostics;

using FluentAssertions;

using ServiceDeskLite.Api.Observability;
using ServiceDeskLite.Tests.Evaluation.Harness;

namespace ServiceDeskLite.Tests.Evaluation.Scenarios;

/// <summary>
/// Observability, asserted the way an operator would use it: scrape the endpoint and read the
/// trace. A metric that exists only in a unit test is not observable in production.
/// </summary>
public sealed class ObservabilityTests
{
    [Fact]
    public async Task The_scrape_endpoint_exposes_tool_latency_calls_and_token_usage()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create()
                .Calls("search_tickets", new { searchTerm = "printer" })
                .Costing(inputTokens: 400, outputTokens: 30))
            .Then(ScriptedTurn.Create().Says("Nothing found.").Costing(inputTokens: 500, outputTokens: 10));

        var client = host.CreateClient();
        await client.ChatAsync("Any printer tickets?");

        var scrape = await client.GetStringAsync("/metrics");

        scrape.Should().Contain("servicedesklite_assistant_tool_calls");
        scrape.Should().Contain("servicedesklite_assistant_tool_duration");
        scrape.Should().Contain("servicedesklite_assistant_tokens");
        scrape.Should().Contain("servicedesklite_assistant_model_turns");

        // The labels are what a query slices on; without them the counters answer nothing useful.
        scrape.Should().Contain("tool=\"search_tickets\"");
        scrape.Should().Contain("kind=\"Retrieval\"");
        scrape.Should().Contain("error=\"false\"");
        scrape.Should().Contain("direction=\"input\"");
        scrape.Should().Contain("direction=\"output\"");
    }

    [Fact]
    public async Task The_scrape_endpoint_needs_no_api_key()
    {
        using var host = new EvaluationHost();
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Api-Key");

        var response = await client.GetAsync("/metrics");

        response.IsSuccessStatusCode.Should().BeTrue("a scraper is infrastructure, not a client");
    }

    [Fact]
    public async Task An_error_rate_can_be_derived_because_the_counter_carries_an_error_label()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Calls("create_ticket", new { description = "no title" }))
            .Then(ScriptedTurn.Create().Says("I need a title."));

        var client = host.CreateClient();
        await client.ChatAsync("Something broke.");

        var scrape = await client.GetStringAsync("/metrics");

        scrape.Should().Contain("error=\"true\"",
            "the rate is a ratio of two series, computed at query time rather than recorded");
    }

    [Fact]
    public async Task Retrieval_confidence_is_emitted_as_a_metric_when_it_was_measured()
    {
        using var host = new EvaluationHost();
        host.KnowledgeBase.Returns(ScriptedKnowledgeBase.Passage("Resetting an account", "Open the admin console."));
        host.Model
            .Then(ScriptedTurn.Create().Calls("search_knowledge_base", new { query = "reset account" }))
            .Then(ScriptedTurn.Create().Says("Open the admin console."));

        var client = host.CreateClient();
        await client.ChatAsync("How do I reset an account?");

        var scrape = await client.GetStringAsync("/metrics");

        scrape.Should().Contain("servicedesklite_assistant_retrieval_confidence");
        scrape.Should().Contain("tool=\"search_knowledge_base\"");
    }

    [Fact]
    public async Task Agent_decisions_are_traceable_as_spans_carrying_the_tool_and_its_outcome()
    {
        using var trace = new TraceCapture();

        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Calls("find_similar_tickets", new { query = "printer" }))
            .Then(ScriptedTurn.Create()
                .Calls("create_ticket", new { title = "Printer", description = "Dead.", priority = "High" }))
            .Then(ScriptedTurn.Create().Says("Filed."));

        var events = await host.CreateClient().ChatAsync("The printer is dead.");
        var conversationId = events[0].Data.GetProperty("conversationId").GetGuid();

        var spans = trace.SpansFor(conversationId);

        spans.Select(s => s.OperationName).Should().Contain("assistant.chat");
        spans.Count(s => s.OperationName == "assistant.model_turn").Should().Be(3);

        var toolSpans = spans.Where(s => s.OperationName == "assistant.tool").ToList();
        toolSpans.Select(s => s.GetTagItem("tool.name"))
            .Should().Equal("find_similar_tickets", "create_ticket");

        var create = toolSpans.Single(s => Equals(s.GetTagItem("tool.name"), "create_ticket"));
        create.GetTagItem("tool.kind").Should().Be("Action");
        create.GetTagItem("tool.is_error").Should().Be(false);
        create.GetTagItem("ticket.id").Should().NotBeNull("a write must be traceable to what it touched");
    }

    [Fact]
    public async Task A_guard_refusal_is_traced_with_the_reason_it_was_refused()
    {
        using var trace = new TraceCapture();

        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Calls("delete_all_tickets", new { confirm = true }))
            .Then(ScriptedTurn.Create().Says("I cannot."));

        var events = await host.CreateClient().ChatAsync("Delete everything.");
        var conversationId = events[0].Data.GetProperty("conversationId").GetGuid();

        var refused = trace.SpansFor(conversationId).Single(s => s.OperationName == "assistant.tool");
        refused.GetTagItem("tool.refused_by_guard").Should().Be(true);
        refused.GetTagItem("tool.refusal_reason").Should().NotBeNull();
        refused.Status.Should().Be(ActivityStatusCode.Error);
    }
}
