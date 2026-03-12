using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.GetTicketById;

internal static class TicketDetailsDtoMapping
{
    public static TicketDetailsDto ToDetailsDto(this Ticket ticket, DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var allowedTransitions = TicketWorkflow.GetAllowedTransitions(ticket.Status);

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
            allowedTransitions,
            IsOverdue: ticket.DueAt is not null && ticket.DueAt.Value < utcNow
                && ticket.Status is not TicketStatus.Resolved and not TicketStatus.Closed,
            DisplayRef: $"#{ticket.Id.Value:N}"[..7].ToUpperInvariant(),
            StatusGuidance: BuildStatusGuidance(ticket.Status),
            SuggestedNextSteps: BuildSuggestedNextSteps(ticket, allowedTransitions));
    }

    private static string BuildStatusGuidance(TicketStatus status)
        => status switch
        {
            TicketStatus.New        => "Review the request, confirm the issue statement, and move it into triage once it is ready for routing.",
            TicketStatus.Triaged    => "The ticket is ready for assignment, progress work, or direct resolution if the outcome is already clear.",
            TicketStatus.InProgress => "Active work is underway. Keep the latest context in the conversation thread and track the due date closely.",
            TicketStatus.Waiting    => "The ticket is currently blocked by an external dependency. Document the blocker and move it forward once a response arrives.",
            TicketStatus.Resolved   => "Resolution is recorded. Confirm the outcome and close the ticket when no further action is needed.",
            TicketStatus.Closed     => "The workflow is complete. Use history and comments as the factual record of what happened.",
            _                       => string.Empty
        };

    private static IReadOnlyList<string> BuildSuggestedNextSteps(
        Ticket ticket,
        IReadOnlyList<TicketStatus> allowedTransitions)
    {
        var suggestions = new List<string>();

        if (ticket.Assignee is null && ticket.Status is not TicketStatus.Closed)
            suggestions.Add("Assign an owner so responsibility and next handling steps are explicit.");

        if (ticket.DueAt is null && ticket.Status is not TicketStatus.Resolved and not TicketStatus.Closed)
            suggestions.Add("Add a due date if the ticket should be tracked against a service target or external commitment.");

        if (allowedTransitions.Count > 0)
            suggestions.Add($"Prepare the next workflow move: {string.Join(", ", allowedTransitions.Select(FormatStatus))}.");

        if (ticket.Status == TicketStatus.Waiting)
            suggestions.Add("Record the blocker clearly in the notes and move the ticket back to In Progress once the dependency responds.");

        if (ticket.Status == TicketStatus.Resolved)
            suggestions.Add("Validate the outcome before closing, or reopen to In Progress if follow-up work is required.");

        if (suggestions.Count == 0)
            suggestions.Add("No immediate follow-up is suggested. Use comments and history for recordkeeping.");

        return suggestions;
    }

    private static string FormatStatus(TicketStatus status)
        => status == TicketStatus.InProgress ? "In Progress" : status.ToString();
}
