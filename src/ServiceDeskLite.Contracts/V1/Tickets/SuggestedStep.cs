namespace ServiceDeskLite.Contracts.V1.Tickets;

/// <summary>
/// Which manual workflow action a suggested next step maps to. <see cref="None"/> means the
/// step is advisory only and has no one-click action, so the client shows it as text rather
/// than a dead button.
/// </summary>
public enum SuggestedActionKind
{
    None,
    ChangeStatus,
    Assign,
    Comment
}

/// <summary>
/// A recommended next step for a ticket. <see cref="Text"/> is the human-readable advice;
/// <see cref="Action"/> (with <see cref="TargetStatus"/> for a status change) tells the client
/// which existing command handler the step routes to. The server decides the mapping because it
/// knows why the step was suggested - the client never parses the prose.
/// </summary>
public sealed record SuggestedStep(
    string Text,
    SuggestedActionKind Action,
    TicketStatus? TargetStatus);
