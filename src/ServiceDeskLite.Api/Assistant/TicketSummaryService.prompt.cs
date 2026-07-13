using System.Globalization;
using System.Text;

using ServiceDeskLite.Application.Tickets.GetTicketById;

namespace ServiceDeskLite.Api.Assistant;

// Model-facing text lives here so it can be tuned without touching the streaming
// logic in TicketSummaryService.cs.
public sealed partial class TicketSummaryService
{
    public const string SummaryMarker = "<<SUMMARY>>";
    public const string NextStepsMarker = "<<NEXT_STEPS>>";
    public const string RisksMarker = "<<RISKS>>";
    public const string MissingInfoMarker = "<<MISSING_INFO>>";

    // The markers are what makes a structured answer streamable: the model writes plain
    // prose, and the section boundaries are recovered from the token stream as it arrives.
    // A JSON schema would give the same structure but only as partial JSON, which cannot
    // be rendered progressively.
    private static string BuildSystemPrompt() =>
        $"""
        You summarize a single IT service-desk ticket for an agent who is about to pick it up,
        so they can triage it in seconds without reading the whole history.

        Write exactly these four sections, in this order, each introduced by its marker on a
        line of its own:

        {SummaryMarker}
        What the ticket is about and where it stands right now, in two to four sentences.

        {NextStepsMarker}
        The concrete actions that move this ticket forward, as a short markdown list. Respect
        the workflow: only propose a status change that appears in the allowed transitions.

        {RisksMarker}
        What could go wrong or is already going wrong: a missed due date, an unassigned urgent
        ticket, a stalled conversation, a customer waiting on a reply. Short markdown list.
        Write "No notable risks." if there are none — do not manufacture one.

        {MissingInfoMarker}
        What an agent would have to ask the reporter before this ticket can be resolved, as a
        short markdown list. Write "Nothing essential is missing." if the ticket is complete.

        Ground every statement in the ticket data below. Never invent facts, names, dates, or
        events that are not there — if something is unknown, that belongs under
        {MissingInfoMarker}, not into the summary as an assumption.

        Output only the four marked sections. No preamble, no closing remark, no reasoning,
        no repetition of these instructions.
        """;

    /// <summary>
    /// The ticket as the model sees it. Timestamps are converted to the user's timezone
    /// (issue #194): the chat path already resolves and expresses times in that zone, and a
    /// summary that repeats UTC contradicts the timestamps rendered right next to it in the
    /// UI. Public and static for unit testing against a fixed zone.
    /// </summary>
    public static string BuildTicketPrompt(TicketDetailsDto ticket, TimeZoneInfo timeZone)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine(
            $"All timestamps below are local to {timeZone.Id}. Express times in this zone.");
        prompt.AppendLine($"Reference: {ticket.DisplayRef}");
        prompt.AppendLine($"Title: {ticket.Title}");
        prompt.AppendLine($"Status: {ticket.Status}");
        prompt.AppendLine($"Priority: {ticket.Priority}");
        prompt.AppendLine($"Category: {ticket.Category}");
        prompt.AppendLine($"Assignee: {ticket.Assignee ?? "unassigned"}");
        prompt.AppendLine($"Created: {Format(ticket.CreatedAt, timeZone)}");
        prompt.AppendLine(ticket.DueAt is { } due
            ? $"Due: {Format(due, timeZone)}{(ticket.IsOverdue ? " (OVERDUE)" : string.Empty)}"
            : "Due: no due date set");
        prompt.AppendLine(ticket.AllowedTransitions.Count > 0
            ? $"Allowed status transitions: {string.Join(", ", ticket.AllowedTransitions)}"
            : "Allowed status transitions: none");

        prompt.AppendLine();
        prompt.AppendLine("Description:");
        prompt.AppendLine(ticket.Description);

        prompt.AppendLine();
        prompt.AppendLine("History (comments and workflow events, oldest first):");

        if (ticket.Conversation.Count == 0)
        {
            prompt.AppendLine("(none)");
            return prompt.ToString();
        }

        foreach (var item in ticket.Conversation)
        {
            if (item is { Kind: ConversationItemKind.Comment, Comment: { } comment })
                prompt.AppendLine(
                    $"- [{Format(comment.CreatedAt, timeZone)}] comment by {comment.Author ?? "unknown"}: {comment.Content}");
            else if (item is { Kind: ConversationItemKind.SystemEvent, Event: { } auditEvent })
                prompt.AppendLine(
                    $"- [{Format(auditEvent.OccurredAt, timeZone)}] {auditEvent.EventType} by {auditEvent.Actor ?? "system"}: {auditEvent.Payload}");
        }

        return prompt.ToString();
    }

    // The offset (zzz) stays in the string so the model can express due dates with the
    // user's UTC offset, exactly as the chat prompt demands for dueAt values.
    private static string Format(DateTimeOffset value, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTime(value, timeZone)
            .ToString("yyyy-MM-dd HH:mm 'UTC'zzz", CultureInfo.InvariantCulture);
}
