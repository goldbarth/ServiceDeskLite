using System.Globalization;
using System.Text;

using ServiceDeskLite.Application.Tickets.GetTicketById;

namespace ServiceDeskLite.Api.Worker;

public sealed partial class TicketReviewer
{
    /// <summary>
    /// What the worker is told about its own situation. Deliberately blunt about the one thing that
    /// separates it from the chat assistant: nobody is reading this as it happens.
    /// </summary>
    private const string SystemPrompt =
        """
        You are the autonomous ticket worker of a service desk. You are reviewing one ticket on a
        scheduled background scan. There is no user in this conversation: nothing you write here is
        read by anyone. The only way to reach a person is the add_comment tool, which writes on the
        ticket itself.

        Work in this order:

        1. Read the ticket. Decide whether it has enough information to be worked on. A ticket is
           missing information when someone picking it up would have to ask before they could start
           — no error message, no affected system, no steps taken, no way to reproduce.
        2. If information is missing, ask for it with add_comment: name precisely what is missing
           and why you need it. Then park the ticket in Waiting with change_ticket_status, so nobody
           picks it up before the answer arrives. The workflow only allows the transitions listed
           below for this ticket: a New ticket has to go to Triaged first, and can be moved to
           Waiting on the next scan. Stop there.
        3. Otherwise, look for a solution. Use search_knowledge_base for how-to and policy questions
           and find_similar_tickets to see how a comparable ticket was handled. If you draft an
           answer from knowledge-base passages, verify it with check_grounding before you post it.
           Post a well-grounded proposal with add_comment. Do not post a proposal you could not
           ground — say what you found and what you could not confirm instead.
        4. If the ticket appears already resolved, or should be closed, or a separate ticket ought to
           be opened, do not do it. Propose it with add_comment, with your reasoning, and let a
           person decide.

        Rules:

        - Never guess a fact about the ticket, the system, or the customer. If the ticket does not
          say it and no source confirms it, you do not know it.
        - Actions that need human review come back to you refused. That is expected, not an error.
          Do not retry them: post your proposal as a comment instead.
        - Write comments for the person who owns the ticket. State what you found, what you propose,
          and what you are unsure about. Never describe your own tool calls.
        - One comment per scan. If you have already commented on this ticket, and nothing has changed
          since, say nothing and stop.
        - Be brief. An agent reads these between other work.
        """;

    /// <summary>
    /// The ticket as the model first sees it, including the comments so far — without them it would
    /// ask on every scan the question it already asked on the last one.
    /// </summary>
    private static string BuildTicketPrompt(TicketDetailsDto ticket, DateTimeOffset now)
    {
        var age = now - ticket.CreatedAt;

        var prompt = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"Ticket {ticket.Id.Value} — review it now.")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"Title: {ticket.Title}")
            .AppendLine(CultureInfo.InvariantCulture, $"Status: {ticket.Status}")
            .AppendLine(CultureInfo.InvariantCulture, $"Priority: {ticket.Priority}")
            .AppendLine(CultureInfo.InvariantCulture, $"Category: {ticket.Category}")
            .AppendLine(CultureInfo.InvariantCulture, $"Assignee: {ticket.Assignee ?? "nobody"}")
            .AppendLine(CultureInfo.InvariantCulture, $"Opened: {age.TotalHours:F0} hours ago")
            // The domain state machine is authoritative and rejects anything else. Stating the legal
            // moves is cheaper than letting the model guess and read the rejection back.
            .AppendLine(CultureInfo.InvariantCulture,
                $"Allowed status transitions from here: {(ticket.AllowedTransitions.Count > 0
                    ? string.Join(", ", ticket.AllowedTransitions)
                    : "none")}")
            .AppendLine()
            .AppendLine("Description:")
            .AppendLine(ticket.Description);

        var comments = ticket.Conversation
            .Where(item => item.Kind is ConversationItemKind.Comment && item.Comment is not null)
            .Select(item => item.Comment!)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        if (comments.Count > 0)
        {
            prompt.AppendLine().AppendLine("Comments so far, oldest first:");
            foreach (var comment in comments)
                prompt.AppendLine(CultureInfo.InvariantCulture,
                    $"- [{comment.CreatedAt:yyyy-MM-dd HH:mm}] {comment.Author ?? "unknown"}: {comment.Content}");
        }
        else
        {
            prompt.AppendLine().AppendLine("No comments yet.");
        }

        return prompt.ToString();
    }
}
