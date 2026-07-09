using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Api.Assistant.Agent;

/// <summary>
/// What the agent did, as it happens. The chat adapter turns these into SSE events for a browser;
/// the autonomous worker logs and audits them. Neither the loop nor a tool knows which is reading.
/// </summary>
public abstract record AgentLoopEvent;

/// <summary>A fragment of the model's answer, as it streams.</summary>
public sealed record AgentTextEvent(string Text) : AgentLoopEvent;

/// <summary>The model asked for a tool. Emitted before the tool runs.</summary>
public sealed record AgentToolCallEvent(string ToolName) : AgentLoopEvent;

/// <summary>A tool finished, or a guard refused it. <paramref name="Result"/> carries which.</summary>
public sealed record AgentToolResultEvent(string ToolName, ToolResult Result) : AgentLoopEvent;

/// <summary>
/// The run ended and the model produced its answer. <paramref name="Text"/> is everything it said
/// across all tool iterations, which is what a caller persists.
/// </summary>
public sealed record AgentCompletedEvent(string Text) : AgentLoopEvent;

/// <summary>
/// The run ended without an answer. The loop never throws across this boundary: an upstream fault,
/// an exhausted budget, and a refused-everything turn all arrive here, phrased for whoever reads.
/// </summary>
public sealed record AgentErrorEvent(string Message) : AgentLoopEvent;

/// <param name="Owner">Whose sandbox budgets this run spends.</param>
/// <param name="Seed">The conversation so far; the loop appends to it as the model works.</param>
/// <param name="MaxTokens">Output ceiling per model turn.</param>
public sealed record AgentRequest(
    string SystemPrompt,
    IReadOnlyList<MessageParam> Seed,
    OwnerId Owner,
    int MaxTokens);
