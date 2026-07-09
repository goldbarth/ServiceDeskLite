using FluentAssertions;

using ServiceDeskLite.Tests.Evaluation.Harness;

namespace ServiceDeskLite.Tests.Evaluation.Scenarios;

/// <summary>
/// The curated prompt suite: each entry pairs a user prompt with the plan the model follows, and
/// pins the observable outcome.
/// </summary>
/// <remarks>
/// What this does and does not evaluate is worth being exact about. The model is scripted, so the
/// suite does not measure the model's judgment — no offline suite can, without calling it. It
/// measures everything the agent does <em>around</em> that judgment: that the requested tools run,
/// through the real handlers, in the right order, that the results come back correctly shaped,
/// that the workspace ends in the expected state, and that the client sees the expected stream.
/// Those are the parts that regress silently when the code changes; the model's judgment regresses
/// visibly, and is checked against the live API by hand.
/// </remarks>
public sealed class PromptSuiteTests
{
    public static TheoryData<AgentScenario> Scenarios =>
    [
        new AgentScenario(
            Name: "Intake: a plain problem report becomes a ticket",
            Prompt: "The printer on the third floor is dead.",
            Turns:
            [
                ScriptedTurn.Create().Calls("create_ticket", new
                {
                    title = "Printer on third floor is dead",
                    description = "Reported by a user; printer unresponsive.",
                    priority = "High",
                }),
                ScriptedTurn.Create().Says("I filed a high-priority ticket."),
            ],
            ExpectedTools: ["create_ticket"],
            ExpectedAnswer: "I filed a high-priority ticket.",
            ExpectedTicketsCreated: 1),

        new AgentScenario(
            Name: "Dedup: an existing ticket stops a second one being filed",
            Prompt: "The printer on the third floor is dead.",
            Turns:
            [
                ScriptedTurn.Create().Calls("find_similar_tickets", new { query = "printer third floor dead" }),
                ScriptedTurn.Create().Says("There is already a ticket for this. I did not file a duplicate."),
            ],
            ExpectedTools: ["find_similar_tickets"],
            ExpectedAnswer: "There is already a ticket for this. I did not file a duplicate.",
            ExpectedTicketsCreated: 0),

        new AgentScenario(
            Name: "Clarification: a vague deadline is questioned, not guessed",
            Prompt: "Fix the printer by Friday morning.",
            Turns: [ScriptedTurn.Create().Says("What time on Friday morning should I set as the deadline?")],
            ExpectedTools: [],
            ExpectedAnswer: "What time on Friday morning should I set as the deadline?",
            ExpectedTicketsCreated: 0),

        new AgentScenario(
            Name: "Lookup: an existing ticket is found rather than recreated",
            Prompt: "Is anyone working on the VPN problem?",
            Turns:
            [
                ScriptedTurn.Create().Calls("search_tickets", new { searchTerm = "VPN" }),
                ScriptedTurn.Create().Says("No open VPN tickets."),
            ],
            ExpectedTools: ["search_tickets"],
            ExpectedAnswer: "No open VPN tickets.",
            ExpectedTicketsCreated: 0),

        new AgentScenario(
            Name: "Chain: dedup check, then file",
            Prompt: "The coffee machine is leaking.",
            Turns:
            [
                ScriptedTurn.Create().Calls("find_similar_tickets", new { query = "coffee machine leaking" }),
                ScriptedTurn.Create().Calls("create_ticket", new
                {
                    title = "Coffee machine leaking",
                    description = "Water on the floor of the kitchenette.",
                    priority = "Low",
                }),
                ScriptedTurn.Create().Says("No duplicate found, so I filed it."),
            ],
            ExpectedTools: ["find_similar_tickets", "create_ticket"],
            ExpectedAnswer: "No duplicate found, so I filed it.",
            ExpectedTicketsCreated: 1),
    ];

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Scenario_produces_the_expected_tools_answer_and_workspace(AgentScenario scenario)
    {
        using var host = new EvaluationHost();
        foreach (var turn in scenario.Turns)
            host.Model.Then(turn);

        var client = host.CreateClient();
        var before = await client.CountTicketsAsync();

        var events = await client.ChatAsync(scenario.Prompt);

        events.OfType(AgentTranscript.ToolCallEvent).Select(e => e.ToolName)
            .Should().Equal(scenario.ExpectedTools, "the plan must reach the handlers unchanged");

        events.AnswerOf().Should().Be(scenario.ExpectedAnswer);
        events[^1].Type.Should().Be(AgentTranscript.DoneEvent);

        var after = await client.CountTicketsAsync();
        (after - before).Should().Be(scenario.ExpectedTicketsCreated,
            "a scenario that files nothing must leave no trace");
    }

    [Fact]
    public async Task Every_write_the_agent_makes_is_audited_as_the_assistant()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Calls("create_ticket", new
            {
                title = "Audit check",
                description = "Filed by the agent.",
                priority = "Medium",
            }))
            .Then(ScriptedTurn.Create().Says("Filed."));

        var client = host.CreateClient();
        await client.ChatAsync("Something broke.");

        var ticketId = await client.FindTicketIdAsync("Audit check");
        var audit = await client.AuditEventsAsync(ticketId);

        audit.Should().ContainSingle()
            .Which.GetProperty("actor").GetString().Should().Be("ai-assistant",
                "the agent writes through the same handlers, so it is audited like any other actor");
    }
}

/// <param name="Turns">What the model decides, in order. One entry per model round trip.</param>
/// <param name="ExpectedTicketsCreated">Change in ticket count; the scenario's effect on the workspace.</param>
public sealed record AgentScenario(
    string Name,
    string Prompt,
    IReadOnlyList<ScriptedTurn> Turns,
    IReadOnlyList<string> ExpectedTools,
    string ExpectedAnswer,
    int ExpectedTicketsCreated)
{
    public override string ToString() => Name;
}
