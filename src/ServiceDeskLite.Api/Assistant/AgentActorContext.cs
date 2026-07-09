using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Domain.Audit;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Which agent the tools in this scope are serving. Interactive unless the autonomous worker says
/// otherwise, and it may only say so for the scope it owns.
/// </summary>
/// <remarks>
/// Scoped, and mutable exactly once, because the container cannot swap a scoped registration per
/// scope. Every request scope is therefore the assistant by construction — the only way to get the
/// autonomous actor is to open a scope and ask for it, which is what
/// <see cref="Worker.TicketWorker"/> does per ticket and nothing else does at all.
/// <para>
/// Two things read this: every write tool, to name the actor in the audit trail, and
/// <see cref="Sandbox.HumanReviewGuard"/>, to decide whether an action needed a human first.
/// </para>
/// </remarks>
public sealed class AgentActorContext : IAgentActor
{
    public string Actor => Mode is AgentMode.Autonomous ? AuditActors.AiWorker : AuditActors.AiAssistant;

    public AgentMode Mode { get; private set; } = AgentMode.Interactive;

    /// <summary>Marks this scope as the unattended worker. Called once, when the worker opens it.</summary>
    public void RunAutonomously() => Mode = AgentMode.Autonomous;
}
