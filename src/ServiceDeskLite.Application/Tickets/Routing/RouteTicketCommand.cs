using ServiceDeskLite.Application.Abstractions.Routing;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.Routing;

/// <summary>Auto-route (triage) an existing ticket from its content.</summary>
public sealed record RouteTicketCommand(TicketId Id, string? Actor = null);

/// <summary>
/// Result of a routing run. <see cref="Applied"/> is true when the decision cleared the
/// confidence gate and at least one change was made through the command handlers;
/// otherwise the <see cref="Decision"/> is returned as a suggestion only.
/// <see cref="AppliedChanges"/> is a human-readable list of what changed, for the model
/// and the caller.
/// </summary>
public sealed record RouteTicketResult(
    bool Applied,
    RoutingDecision Decision,
    IReadOnlyList<string> AppliedChanges);
