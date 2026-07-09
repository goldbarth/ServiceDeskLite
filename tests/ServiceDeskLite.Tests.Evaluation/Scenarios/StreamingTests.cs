using System.Net;

using FluentAssertions;

using ServiceDeskLite.Tests.Evaluation.Harness;

namespace ServiceDeskLite.Tests.Evaluation.Scenarios;

/// <summary>
/// The stream as the browser receives it: event names, their order, and what happens when the
/// upstream API fails. These assertions are on the wire, not on the service, because the wire is
/// the contract the web client is written against.
/// </summary>
public sealed class StreamingTests
{
    [Fact]
    public async Task Text_reaches_the_client_as_many_deltas_not_one_block()
    {
        using var host = new EvaluationHost();
        host.Model.Then(ScriptedTurn.Create().Says("I have filed the ticket for you."));

        var events = await host.CreateClient().ChatAsync("Help.");

        events.OfType(AgentTranscript.TextEvent).Should().HaveCountGreaterThan(1,
            "a client that only renders on completion would still pass a single-delta test");
        events.AnswerOf().Should().Be("I have filed the ticket for you.");
    }

    [Fact]
    public async Task The_conversation_id_arrives_before_any_text()
    {
        using var host = new EvaluationHost();
        host.Model.Then(ScriptedTurn.Create().Says("Hello."));

        var events = await host.CreateClient().ChatAsync("Hi.");

        events[0].Type.Should().Be(AgentTranscript.ConversationEvent,
            "the client needs the id before it can send a second turn");
        events[0].Data.GetProperty("conversationId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_stream_always_terminates_with_done()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Calls("search_tickets", new { searchTerm = "printer" }))
            .Then(ScriptedTurn.Create().Says("Found nothing."));

        var events = await host.CreateClient().ChatAsync("Any printer tickets?");

        events[^1].Type.Should().Be(AgentTranscript.DoneEvent);
        events.OfType(AgentTranscript.ErrorEvent).Should().BeEmpty();
    }

    [Fact]
    public async Task An_upstream_failure_becomes_an_error_event_rather_than_a_broken_stream()
    {
        using var host = new EvaluationHost();
        host.Model.FailsWith(HttpStatusCode.InternalServerError);

        var events = await host.CreateClient().ChatAsync("Hello.");

        events[^1].Type.Should().Be(AgentTranscript.ErrorEvent);
        events[^1].Message.Should().Contain("unavailable");
        events.OfType(AgentTranscript.DoneEvent).Should().BeEmpty("the turn did not complete");
    }

    [Fact]
    public async Task An_unparseable_upstream_response_also_becomes_an_error_event()
    {
        // The API is not contractually obliged to return what the SDK expects. Whatever arrives,
        // a half-written response with an exception thrown into it is not an acceptable outcome.
        using var host = new EvaluationHost();
        host.Model.RespondsWithGarbage();

        var events = await host.CreateClient().ChatAsync("Hello.");

        events[^1].Type.Should().Be(AgentTranscript.ErrorEvent);
        events.OfType(AgentTranscript.DoneEvent).Should().BeEmpty();
    }

    [Fact]
    public async Task Tool_activity_is_surfaced_while_the_stream_stays_open()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Says("Checking.").Calls("search_tickets", new { searchTerm = "vpn" }))
            .Then(ScriptedTurn.Create().Says("Nothing found."));

        var events = await host.CreateClient().ChatAsync("Any VPN tickets?");

        // Text before the tool call, tool events in the middle, text after: one open stream.
        var types = events.EventTypes().ToList();
        types.IndexOf(AgentTranscript.ToolCallEvent).Should().BeGreaterThan(types.IndexOf(AgentTranscript.TextEvent));
        types.LastIndexOf(AgentTranscript.TextEvent).Should().BeGreaterThan(types.IndexOf(AgentTranscript.ToolResultEvent));
    }

    [Fact]
    public async Task A_second_turn_continues_the_server_side_transcript()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Says("Hello."))
            .Then(ScriptedTurn.Create().Says("Still here."));

        var client = host.CreateClient();

        var first = await client.ChatAsync("Hi.");
        var conversationId = first[0].Data.GetProperty("conversationId").GetGuid();

        await client.ChatAsync("Are you there?", conversationId);

        // The client resent only the new message; the server replayed the history.
        var secondRequest = host.Model.Requests[1];
        var messages = secondRequest.GetProperty("messages").EnumerateArray().ToList();
        messages.Should().HaveCount(3, "user, assistant, then the new user message");
        messages[0].GetProperty("content").GetString().Should().Be("Hi.");
        messages[1].GetProperty("content").GetString().Should().Be("Hello.");
        messages[2].GetProperty("content").GetString().Should().Be("Are you there?");
    }
}
