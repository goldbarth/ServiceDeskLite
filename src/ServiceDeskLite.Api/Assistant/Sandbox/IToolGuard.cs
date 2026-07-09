using System.Text.Json;

using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Api.Assistant.Sandbox;

/// <summary>
/// One admission rule applied to every tool call before it reaches a command handler.
/// </summary>
/// <remarks>
/// <see cref="Check"/> must be free of side effects, and <see cref="Commit"/> is called only
/// once every guard has admitted the call. The split exists so a budget is never spent on a
/// call that a later guard refuses: without it, whichever guard runs first would consume its
/// allowance and then watch the call be rejected anyway.
/// </remarks>
public interface IToolGuard
{
    /// <summary>Decides whether the call may proceed. No state may change here.</summary>
    ToolGuardResult Check(ToolInvocationContext context);

    /// <summary>Spends whatever the guard tracks. Called only after every guard admitted the call.</summary>
    void Commit(ToolInvocationContext context) { }
}

/// <param name="Reason">
/// Why the call was refused, phrased for the model: it goes back as the <c>tool_result</c>
/// content and is the only thing the model learns about the sandbox.
/// </param>
public readonly record struct ToolGuardResult(bool IsAllowed, string? Reason)
{
    public static ToolGuardResult Allow() => new(true, null);

    public static ToolGuardResult Deny(string reason) => new(false, reason);
}

/// <param name="Input">Raw tool arguments as the model produced them, before any tool parses them.</param>
/// <param name="Turn">Counters scoped to one chat turn; shared by every call within it.</param>
/// <param name="Mode">
/// Whether a human is watching. The same call is a different risk depending on the answer: a user
/// who asked for a ticket to be closed has already approved it, while a background loop closing one
/// has approved nothing.
/// </param>
public sealed record ToolInvocationContext(
    string ToolName,
    JsonElement Input,
    OwnerId Owner,
    ToolTurnState Turn,
    AgentMode Mode = AgentMode.Interactive);

/// <summary>
/// Mutable per-turn accounting. One instance lives for the length of a single
/// <see cref="AssistantChatService.StreamChatAsync"/> call, so a budget cannot leak between
/// turns and a guard can stay a singleton.
/// </summary>
public sealed class ToolTurnState
{
    public int Writes { get; private set; }

    public void CountWrite() => Writes++;
}
