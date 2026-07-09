namespace ServiceDeskLite.Api.Assistant;

public sealed partial class AddCommentTool
{
    private const string ToolDescription =
        """
        Add a comment to an existing ticket. Use it to ask the reporter for information the ticket
        is missing, to record a proposed solution, or to explain an action you are recommending but
        may not take yourself. A comment changes nothing about the ticket — it is how you reach the
        person who owns it. Requires the ticket id, from an earlier create_ticket result or resolved
        with search_tickets first.

        Write the comment for the person who will read it: state what is missing or what you
        propose, and why. Do not describe your own tool calls.
        """;

    private const string TicketIdDescription =
        "Id of the ticket to comment on, from a create_ticket or search_tickets result.";

    private const string ContentDescription =
        "The comment text, addressed to the person who owns the ticket.";
}
