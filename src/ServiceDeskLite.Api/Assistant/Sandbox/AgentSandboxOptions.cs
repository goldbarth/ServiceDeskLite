namespace ServiceDeskLite.Api.Assistant.Sandbox;

/// <summary>
/// Bounds on what the assistant may do (ADR-0035). Every value is a safety net, not a business
/// rule: a well-behaved conversation never reaches one.
/// </summary>
public sealed class AgentSandboxOptions
{
    public const string SectionName = "AgentSandbox";

    /// <summary>
    /// Maximum size of one tool's raw JSON arguments. Generous next to any legitimate call, so a
    /// breach means the model is echoing a ticket back rather than referencing it.
    /// </summary>
    public int MaxInputCharacters { get; init; } = 16_384;

    /// <summary>
    /// Maximum length of any single string inside those arguments. Below the domain's own limits,
    /// so a value that passes here can still be rejected by the handler that owns the rule.
    /// </summary>
    public int MaxStringCharacters { get; init; } = 8_000;

    /// <summary>
    /// Maximum ticket- or memory-changing tool calls per chat turn. A full autonomous chain
    /// (create, route, assign) needs three; the headroom covers a correction round.
    /// </summary>
    public int MaxWritesPerTurn { get; init; } = 6;

    /// <summary>Tool calls one owner may make per minute, refilled continuously.</summary>
    public int ToolCallsPerMinute { get; init; } = 60;

    /// <summary>
    /// Model round trips one owner may make per minute, across chat turns and ticket summaries.
    /// This is the ceiling on what a runaway client can spend.
    /// </summary>
    public int ModelTurnsPerMinute { get; init; } = 30;
}
