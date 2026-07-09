using System.Text.Json;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Api.Worker;
using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Assistant.Sandbox;

/// <summary>
/// Holds back the high-impact actions of an agent that nobody is watching, and tells it to ask
/// instead (ADR-0037).
/// </summary>
/// <remarks>
/// Only <see cref="AgentMode.Autonomous"/> runs are constrained. In a conversation the user is the
/// review: they asked for the ticket to be closed and they see it close. On a background scan
/// nobody asked, so a write that a person would want to have seen first is refused, and the refusal
/// says what to do about it — post the proposal as a comment.
/// <para>
/// A refusal here is not an error. The model reads the reason and comments, which is precisely the
/// outcome the guardrail exists to produce: the reasoning reaches a human, and the ticket is
/// untouched until that human agrees.
/// </para>
/// </remarks>
public sealed class HumanReviewGuard : IToolGuard
{
    private readonly AutonomousWorkerOptions _policy;

    public HumanReviewGuard(IOptions<AutonomousWorkerOptions> policy)
    {
        _policy = policy?.Value ?? throw new ArgumentNullException(nameof(policy));
    }

    public ToolGuardResult Check(ToolInvocationContext context)
    {
        if (context.Mode is not AgentMode.Autonomous)
            return ToolGuardResult.Allow();

        // Reading a ticket, the knowledge base or a memory changes nothing, so it needs no approval.
        if (!ToolCatalog.IsWrite(context.ToolName))
            return ToolGuardResult.Allow();

        if (_policy.AutonomousWrites.Contains(context.ToolName, StringComparer.Ordinal))
            return ToolGuardResult.Allow();

        // Parking a ticket while waiting for an answer is the worker saying "I asked". It resolves
        // nothing, so it is allowed even though the tool that does it can also close a ticket.
        if (context.ToolName == ChangeTicketStatusTool.Name && IsAutonomousTransition(context.Input))
            return ToolGuardResult.Allow();

        return ToolGuardResult.Deny(
            $"'{context.ToolName}' needs human review and was not executed, because you are running "
            + "unattended on a background scan rather than in a conversation. Do not retry it. "
            + $"Use '{AddCommentTool.Name}' on the ticket instead: state what you propose to do and "
            + "why, so the person who owns the ticket can decide.");
    }

    private bool IsAutonomousTransition(JsonElement input)
    {
        if (input.ValueKind is not JsonValueKind.Object
            || !input.TryGetProperty("status", out var statusEl)
            || statusEl.ValueKind is not JsonValueKind.String
            || !Enum.TryParse<TicketStatus>(statusEl.GetString(), ignoreCase: true, out var status))
        {
            // Unparseable input is not an approved transition. The tool will reject it in a moment
            // anyway, with a message that explains the schema better than this guard could.
            return false;
        }

        return _policy.AutonomousStatusTransitions.Contains(status);
    }
}
