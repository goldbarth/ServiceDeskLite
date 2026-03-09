using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.GetTicketById;

internal static class TicketDetailsDtoMapping
{
    public static TicketDetailsDto ToDetailsDto(this Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return new TicketDetailsDto(
            ticket.Id,
            ticket.Title,
            ticket.Description,
            ticket.Status,
            ticket.Priority,
            ticket.CreatedAt,
            ticket.DueAt,
            ticket.Assignee,
            ticket.Comments
                .OrderBy(comment => comment.CreatedAt)
                .Select(comment => new CommentDto(comment.Id, comment.Content, comment.Author, comment.CreatedAt))
                .ToList(),
            TicketWorkflow.GetAllowedTransitions(ticket.Status));
    }
}
