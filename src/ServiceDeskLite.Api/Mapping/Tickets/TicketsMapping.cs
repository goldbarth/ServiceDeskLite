using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Contracts.V1.Tickets;

namespace ServiceDeskLite.Api.Mapping.Tickets;

public static class TicketsMapping
{
    public static TicketResponse ToResponse(this TicketDetailsDto dto)
        => new(
            Id: dto.Id.Value,
            Title: dto.Title,
            Description: dto.Description,
            Priority: dto.Priority.ToContract(),
            Status: dto.Status.ToContract(),
            CreatedAt: dto.CreatedAt,
            DueAt: dto.DueAt,
            Assignee: dto.Assignee?.Name,
            Comments: dto.Comments
                .Select(c => new CommentResponse(c.Id.Value, c.Content, c.Author, c.CreatedAt))
                .ToList(),
            AllowedTransitions: dto.AllowedTransitions
                .Select(status => status.ToContract())
                .ToList(),
            IsOverdue: dto.IsOverdue,
            DisplayRef: dto.DisplayRef,
            StatusGuidance: dto.StatusGuidance,
            SuggestedNextSteps: dto.SuggestedNextSteps
        );

    public static TicketListItemResponse ToListItemResponse(this TicketListItemDto dto)
        => new(
            Id: dto.Id.Value,
            Title: dto.Title,
            Priority: dto.Priority.ToContract(),
            Status: dto.Status.ToContract(),
            CreatedAt: dto.CreatedAt,
            DueAt: dto.DueAt,
            Assignee: dto.Assignee,
            AllowedTransitions: dto.AllowedTransitions.Select(s => s.ToContract()).ToList(),
            IsOverdue: dto.IsOverdue,
            DisplayRef: dto.DisplayRef
        );
}
