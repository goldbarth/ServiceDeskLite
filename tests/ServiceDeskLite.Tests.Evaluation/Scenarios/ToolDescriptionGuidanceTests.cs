using System.Text.RegularExpressions;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant.Agent;

namespace ServiceDeskLite.Tests.Evaluation.Scenarios;

/// <summary>
/// Guards that every model-facing tool description says <em>when</em> to call the tool, not only
/// <em>what</em> it does.
/// </summary>
/// <remarks>
/// <para>
/// <b>The problem.</b> The model, not the harness, decides which tool to reach for. ADR-0038 moved
/// the assistant to Sonnet 5 with thinking disabled, a configuration that reaches for a tool it was
/// not explicitly told to use somewhat less readily than Opus 4.8 did (#186). The failure is silent:
/// the assistant still answers, it just answers from training data instead of the knowledge base, or
/// confirms a change it never made. The manual test pass on <c>6f4297a</c> caught exactly that -
/// "Setze die Priorität auf Critical" produced no <c>update_ticket</c> call while the assistant
/// confirmed the change anyway (#190). So the risk is not confined to the discretionary tools; a
/// plainly imperative write was skipped too.
/// </para>
/// <para>
/// <b>The solution measured here.</b> Anthropic reports a measurable lift when a tool's own
/// description states the condition that should trigger it ("Call this when the user asks about
/// current prices"), not merely the action it performs. That mitigation is model-independent - it
/// helps Opus 4.8 as much as Sonnet 5 - so it is worth locking in regardless of any live
/// measurement. This test is that lock: it fails if a tool ships describing only its mechanics, with
/// no cue for when the model should pick it up.
/// </para>
/// <para>
/// <b>The path to it, and what this test is not.</b> #186 asked for the stronger evidence: a
/// scenario set run against the real model, counting how often each tool fires when it should. That
/// measurement is real, billable per run, and unfit for CI, and the scripted evaluation suite cannot
/// stand in for it - scripting the tool calls would only assert its own script (ADR-0037). Running
/// that measurement was deliberately deferred; this deterministic guard is the CI-safe half that
/// does not need a model at all. It reads the same <see cref="ToolDispatcher.Definitions"/> the agent
/// sends upstream, so it cannot drift from what production ships, and it covers a new tool the moment
/// that tool joins the list.
/// </para>
/// </remarks>
public sealed class ToolDescriptionGuidanceTests
{
    /// <summary>
    /// A call-timing cue: a temporal or conditional word that frames a description as stating the
    /// situation in which to reach for the tool ("when the user asks...", "before creating...",
    /// "after search_knowledge_base..."). This is a floor, not a rubric for good guidance - a
    /// description can clear it and still be weak - but a description with none of these is almost
    /// always describing only mechanics.
    /// </summary>
    private static readonly Regex CallTimingCue =
        new(@"\b(when|whenever|before|after)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static TheoryData<string, string> ToolDescriptions()
    {
        var data = new TheoryData<string, string>();
        foreach (var tool in ToolDispatcher.Definitions)
            data.Add(tool.Name, tool.Description ?? string.Empty);
        return data;
    }

    [Theory]
    [MemberData(nameof(ToolDescriptions))]
    public void Every_tool_description_states_when_to_call_it(string toolName, string description)
    {
        CallTimingCue.IsMatch(description).Should().BeTrue(
            "the description for '{0}' should tell the model when to reach for the tool, not only " +
            "what it does; add a call-timing clause such as \"Use this when ...\" or \"Call this " +
            "before ...\" (see #186)",
            toolName);
    }

    [Fact]
    public void The_guard_covers_every_tool_the_agent_exposes()
    {
        // The theory is only as strong as the list it iterates. If the agent ever sends a tool the
        // dispatcher does not enumerate, the guidance rule would silently stop applying to it.
        ToolDispatcher.Definitions.Should().OnlyHaveUniqueItems(t => t.Name);
        ToolDispatcher.Definitions.Should().HaveCount(12,
            "all twelve assistant tools must run through the same when-to-call guard");
    }
}
