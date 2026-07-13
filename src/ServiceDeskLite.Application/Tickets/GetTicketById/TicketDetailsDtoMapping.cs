using ServiceDeskLite.Application.Tickets.GetAuditEvents;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.GetTicketById;

internal static class TicketDetailsDtoMapping
{
    public static TicketDetailsDto ToDetailsDto(
        this Ticket ticket,
        IReadOnlyList<AuditEventDto> auditEvents,
        DateTimeOffset utcNow,
        string? assigneeName)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var allowedTransitions = TicketWorkflow.GetAllowedTransitions(ticket.Status);
        var conversation = BuildConversation(ticket, auditEvents);

        return new TicketDetailsDto(
            ticket.Id,
            ticket.Title,
            ticket.Description,
            ticket.Status,
            ticket.Priority,
            ticket.Category,
            ticket.CreatedAt,
            ticket.DueAt,
            assigneeName,
            conversation,
            allowedTransitions,
            IsOverdue: ticket.DueAt is not null && ticket.DueAt.Value < utcNow
                && ticket.Status is not TicketStatus.Resolved and not TicketStatus.Closed,
            DisplayRef: TicketReference.Format(ticket.Id),
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

    // Each step is tagged with the manual action it maps to, decided here where the reason for the
    // suggestion is known. Adding a due date is deliberately left unmapped (None): it is a details
    // edit, not one of the routed actions (status change, assign, comment).
    private static IReadOnlyList<SuggestedStepDto> BuildSuggestedNextSteps(
        Ticket ticket,
        IReadOnlyList<TicketStatus> allowedTransitions)
    {
        var suggestions = new List<SuggestedStepDto>();

        // Actionable steps first, so the primary next-step control is a live action rather than a
        // caption: the client makes the first step the primary button. Advisory steps (adding a
        // due date, review reminders) follow, since they route to no one-click action.

        if (ticket.AssignedAgentId is null && ticket.Status is not TicketStatus.Closed)
            suggestions.Add(new(
                "Assign an owner so responsibility and next handling steps are explicit.",
                SuggestedActionKind.Assign));

        if (allowedTransitions.Count > 0)
            suggestions.Add(new(
                $"Prepare the next workflow move: {string.Join(", ", allowedTransitions.Select(FormatStatus))}.",
                SuggestedActionKind.ChangeStatus,
                allowedTransitions[0]));

        if (ticket.Status == TicketStatus.Waiting)
            suggestions.Add(new(
                "Record the blocker clearly in the notes and move the ticket back to In Progress once the dependency responds.",
                SuggestedActionKind.Comment));

        if (ticket.DueAt is null && ticket.Status is not TicketStatus.Resolved and not TicketStatus.Closed)
            suggestions.Add(new(
                "Add a due date if the ticket should be tracked against a service target or external commitment.",
                SuggestedActionKind.None));

        if (ticket.Status == TicketStatus.Resolved)
            suggestions.Add(new(
                "Validate the outcome before closing, or reopen to In Progress if follow-up work is required.",
                SuggestedActionKind.None));

        if (suggestions.Count == 0)
            suggestions.Add(new(
                "No immediate follow-up is suggested. Use comments and history for recordkeeping.",
                SuggestedActionKind.None));

        return suggestions;
    }

    private static string FormatStatus(TicketStatus status)
        => status == TicketStatus.InProgress ? "In Progress" : status.ToString();

    private static IReadOnlyList<ConversationItemDto> BuildConversation(
        Ticket ticket,
        IReadOnlyList<AuditEventDto> auditEvents)
    {
        var items = new List<ConversationItemDto>();

        items.AddRange(ticket.Comments
            .Select(c => new ConversationItemDto(
                c.CreatedAt,
                ConversationItemKind.Comment,
                new CommentDto(c.Id, c.Content, c.Author, c.CreatedAt),
                Event: null)));

        items.AddRange(auditEvents
            .Select(e => new ConversationItemDto(
                e.OccurredAt,
                ConversationItemKind.SystemEvent,
                Comment: null,
                new AuditEventDto(e.Id, e.EventType, e.Actor, e.OccurredAt, e.Payload))));

        return [.. items
            .OrderBy(item => item.Timestamp)
            .ThenBy(item => item.Kind)];
    }
}
