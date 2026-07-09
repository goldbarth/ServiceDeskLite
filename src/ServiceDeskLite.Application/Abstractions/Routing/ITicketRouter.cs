using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Abstractions.Routing;

/// <summary>
/// Triages a ticket from its content into a routing decision (category, priority,
/// assignee, status) with a confidence. Deterministic and side-effect free — an
/// embedding/LLM classifier would sit behind the same port, but the default is
/// rule-based so routing is always available and testable against fixtures
/// (issue #159, ADR-0032). The classifier only *decides*; applying the decision
/// (through the existing command handlers) and the confidence gate live in the
/// use-case, keeping this a pure function.
/// </summary>
public interface ITicketRouter
{
    RoutingDecision Route(string title, string description);
}

/// <summary>
/// A triage suggestion. <see cref="SuggestedAssignee"/> is a roster agent name (null
/// when nothing matched); <see cref="SuggestedStatus"/> is the workflow move routing
/// implies (null = leave as-is). <see cref="Confidence"/> (0..1) gates whether the
/// use-case applies the decision or only suggests it; <see cref="Rationale"/> records
/// why, for the audit trail and the model.
/// </summary>
public sealed record RoutingDecision(
    TicketCategory Category,
    TicketPriority Priority,
    string? SuggestedAssignee,
    TicketStatus? SuggestedStatus,
    double Confidence,
    string Rationale);
