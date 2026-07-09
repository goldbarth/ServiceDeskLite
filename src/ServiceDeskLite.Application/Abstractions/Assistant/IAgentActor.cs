namespace ServiceDeskLite.Application.Abstractions.Assistant;

/// <summary>
/// The audit actor the agent's tools write under, and the mode they run in.
/// </summary>
/// <remarks>
/// The same tools serve two callers: the chat assistant, with a human reading every step, and the
/// autonomous worker, with nobody watching. They must not audit as the same actor, and the sandbox
/// must be able to tell them apart — an action that is unremarkable when a user asked for it is a
/// high-impact action when a background loop decided on it alone.
/// <para>
/// Resolved per scope: the request scope binds the interactive assistant, and the worker opens its
/// own scope per ticket. Nothing downstream of a tool has to know which one it is serving.
/// </para>
/// </remarks>
public interface IAgentActor
{
    /// <summary>Value written to <c>AuditEvent.Actor</c> by every tool that changes state.</summary>
    string Actor { get; }

    AgentMode Mode { get; }
}

public enum AgentMode
{
    /// <summary>A human is in the conversation and sees each action as it happens.</summary>
    Interactive,

    /// <summary>The agent acts on its own schedule. High-impact actions need human review first.</summary>
    Autonomous,
}
