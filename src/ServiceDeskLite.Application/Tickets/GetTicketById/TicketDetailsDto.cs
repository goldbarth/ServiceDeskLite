using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.GetTicketById;

public record TicketDetailsDto(
    TicketId Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    TicketCategory Category,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    string? Assignee,
    IReadOnlyList<ConversationItemDto> Conversation,
    IReadOnlyList<TicketStatus> AllowedTransitions,
    bool IsOverdue,
    string DisplayRef,
    string StatusGuidance,
    IReadOnlyList<SuggestedStepDto> SuggestedNextSteps);
