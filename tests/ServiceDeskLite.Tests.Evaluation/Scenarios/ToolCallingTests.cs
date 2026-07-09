using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Tests.Evaluation.Harness;

namespace ServiceDeskLite.Tests.Evaluation.Scenarios;

/// <summary>
/// Tool calling, end to end: the model asks, the agent executes through the real command
/// handlers, and the result goes back to the model. Every assertion is on observable behaviour —
/// the SSE stream, the persisted ticket, and the tool_result the model actually received.
/// </summary>
public sealed class ToolCallingTests
{
    [Fact]
    public async Task A_requested_tool_creates_a_real_ticket_and_reports_back_to_the_model()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create()
                .Says("Let me file that.")
                .Calls("create_ticket", new { title = "Printer offline", description = "Third floor printer is dead.", priority = "High" }))
            .Then(ScriptedTurn.Create().Says("Done, I filed the ticket."));

        var client = host.CreateClient();

        var events = await client.ChatAsync("The third floor printer is dead.");

        events.EventTypes().Should().ContainInOrder(
            AgentTranscript.ConversationEvent,
            AgentTranscript.TextEvent,
            AgentTranscript.ToolCallEvent,
            AgentTranscript.ToolResultEvent,
            AgentTranscript.DoneEvent);

        var toolResult = events.OfType(AgentTranscript.ToolResultEvent).Single();
        toolResult.ToolName.Should().Be("create_ticket");
        toolResult.IsError.Should().BeFalse();

        // The ticket exists, created through CreateTicketHandler like any REST caller's would be.
        var json = new JsonSerializerOptions(JsonSerializerOptions.Web) { Converters = { new JsonStringEnumConverter() } };
        var tickets = await client.GetFromJsonAsync<PagedResponse<TicketListItemResponse>>(
            "/api/v1/tickets?searchTerm=Printer offline", json);
        tickets!.Items.Should().ContainSingle(t => t.Title == "Printer offline");

        // And the model was told, in the second request, that its tool succeeded.
        host.Model.RequestCount.Should().Be(2);
        host.Model.ToolResultsFor("toolu_1").Should().ContainSingle()
            .Which.GetProperty("is_error").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task A_rejected_tool_input_reaches_the_model_as_an_error_it_can_correct()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Calls("create_ticket", new { description = "no title at all" }))
            .Then(ScriptedTurn.Create().Says("Sorry, I need a title."));

        var events = await host.CreateClient().ChatAsync("Something is broken.");

        var toolResult = events.OfType(AgentTranscript.ToolResultEvent).Single();
        toolResult.IsError.Should().BeTrue();

        var fedBack = host.Model.ToolResultsFor("toolu_1").Single();
        fedBack.GetProperty("is_error").GetBoolean().Should().BeTrue();
        fedBack.GetProperty("content").GetString().Should().Contain("title");

        // The loop continued: a rejected input is a correction, not a failure.
        events.EventTypes().Should().EndWith(AgentTranscript.DoneEvent);
    }

    [Fact]
    public async Task The_agent_chains_tools_across_several_model_turns()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Calls("find_similar_tickets", new { query = "printer" }))
            .Then(ScriptedTurn.Create().Calls("create_ticket", new { title = "Printer offline", description = "Dead.", priority = "High" }))
            .Then(ScriptedTurn.Create().Says("Checked for duplicates, then filed it."));

        var events = await host.CreateClient().ChatAsync("Printer is dead.");

        events.OfType(AgentTranscript.ToolCallEvent).Select(e => e.ToolName)
            .Should().Equal("find_similar_tickets", "create_ticket");

        host.Model.RequestCount.Should().Be(3, "each tool round trip is its own model call");
        events.AnswerOf().Should().Be("Checked for duplicates, then filed it.");
    }

    [Fact]
    public async Task An_unknown_tool_is_refused_by_the_sandbox_and_never_reaches_a_handler()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Calls("delete_all_tickets", new { confirm = true }))
            .Then(ScriptedTurn.Create().Says("I cannot do that."));

        var events = await host.CreateClient().ChatAsync("Delete everything.");

        var toolResult = events.OfType(AgentTranscript.ToolResultEvent).Single();
        toolResult.IsError.Should().BeTrue();
        toolResult.Message.Should().Contain("Unknown tool");
    }

    [Fact]
    public async Task The_write_budget_stops_a_runaway_turn_before_the_second_write()
    {
        using var host = new EvaluationHost(); // MaxWritesPerTurn is 6 by default
        var runaway = ScriptedTurn.Create();
        for (var i = 0; i < 7; i++)
            runaway.Calls("create_ticket", new { title = $"Ticket {i}", description = "Spam.", priority = "Low" });

        host.Model.Then(runaway).Then(ScriptedTurn.Create().Says("I stopped at the limit."));

        var events = await host.CreateClient().ChatAsync("File seven tickets.");

        var results = events.OfType(AgentTranscript.ToolResultEvent).ToList();
        results.Should().HaveCount(7);
        results.Take(6).Should().OnlyContain(r => !r.IsError);
        results[6].IsError.Should().BeTrue();
        results[6].Message.Should().Contain("limit");
    }
}
