using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.GetTicketById;

/// <summary>Which manual workflow action a suggested step routes to; <see cref="None"/> is advisory only.</summary>
public enum SuggestedActionKind
{
    None,
    ChangeStatus,
    Assign,
    Comment
}

/// <summary>
/// A recommended next step tagged with the action it maps to. The mapping is decided here, where
/// the reason for the suggestion is known, so the client never has to interpret the prose.
/// </summary>
public sealed record SuggestedStepDto(
    string Text,
    SuggestedActionKind Action,
    TicketStatus? TargetStatus = null);
