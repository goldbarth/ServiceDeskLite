using System.Net.Http.Json;

using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Dashboard;
using ServiceDeskLite.Tests.Evaluation.Harness;

namespace ServiceDeskLite.Tests.Evaluation.Scenarios;

/// <summary>
/// The AI dashboard reads what the agent recorded while it ran. Asserting the two together is the
/// only way to catch a metric that stops being written: the dashboard would keep answering, just
/// with a zero, and a zero is a plausible number.
/// </summary>
public sealed class MetricsRegressionTests
{
    [Fact]
    public async Task A_conversation_is_reflected_in_the_ai_dashboard()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create()
                .Calls("find_similar_tickets", new { query = "printer" })
                .Costing(inputTokens: 500, outputTokens: 40))
            .Then(ScriptedTurn.Create()
                .Calls("create_ticket", new { title = "Printer", description = "Dead.", priority = "High" })
                .Costing(inputTokens: 700, outputTokens: 60))
            .Then(ScriptedTurn.Create()
                .Says("Filed.")
                .Costing(inputTokens: 900, outputTokens: 10));

        var client = host.CreateClient();
        await client.ChatAsync("The printer is dead.");

        var metrics = await client.GetFromJsonAsync<AiDashboardResponse>("/api/v1/dashboard/ai");

        metrics!.Tokens.ModelTurns.Should().Be(3, "each round trip is billed on its own");
        metrics.Tokens.InputTokens.Should().Be(2_100);
        metrics.Tokens.OutputTokens.Should().Be(110);

        metrics.Tools.Should().HaveCount(2);
        metrics.Tools.Should().OnlyContain(t => t.Errors == 0);

        metrics.Retrieval.DuplicateChecks.Should().Be(1);
        metrics.Retrieval.DuplicateChecksWithMatch.Should().Be(0, "the workspace was empty");
        metrics.Retrieval.SemanticAvailable.Should().BeFalse("no embedding provider in the test host");
        metrics.Retrieval.AverageConfidence.Should().BeNull("a keyword-only retrieval reports no score");

        metrics.Automation.AiActions.Should().Be(1, "the agent created one ticket");
        metrics.Automation.Rate.Should().Be(1.0, "no human touched the workspace");
    }

    [Fact]
    public async Task A_refused_tool_call_is_recorded_as_an_error_rather_than_vanishing()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Calls("delete_all_tickets", new { confirm = true }))
            .Then(ScriptedTurn.Create().Says("I cannot do that."));

        var client = host.CreateClient();
        await client.ChatAsync("Delete everything.");

        var metrics = await client.GetFromJsonAsync<AiDashboardResponse>("/api/v1/dashboard/ai");

        var refused = metrics!.Tools.Should().ContainSingle().Subject;
        refused.ToolName.Should().Be("delete_all_tickets");
        refused.Errors.Should().Be(1);
        refused.ErrorRate.Should().Be(1.0);

        metrics.Automation.AiActions.Should().Be(0, "a refused call changes nothing to audit");
    }
}
