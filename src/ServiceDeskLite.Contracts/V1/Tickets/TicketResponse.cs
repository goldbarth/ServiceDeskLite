namespace ServiceDeskLite.Contracts.V1.Tickets;

public sealed record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    TicketPriority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    string? Assignee,
    IReadOnlyList<CommentResponse> Comments,
    IReadOnlyList<TicketStatus> AllowedTransitions,
    bool IsOverdue,
    string DisplayRef,
    string StatusGuidance,
    IReadOnlyList<string> SuggestedNextSteps);
