namespace ServiceDeskLite.Api.Assistant;

public sealed partial class AssignTicketTool
{
    private const string ToolDescription =
        """
        Assign a ticket to an agent, reassign it, or unassign it. Call this when the user asks to
        assign, reassign, or unassign a ticket, or names who should own it. Give the agent's name as
        it appears in the roster (e.g. "Alex Kim"); it is resolved to the roster. If the name
        does not match an active agent, the tool returns the list of valid agents — relay it
        and ask the user to pick one rather than inventing a name. Omit the agent name (or pass
        an empty one) to unassign. Requires the ticket id from an earlier create_ticket result
        or resolved from the user's description with search_tickets first.
        """;

    private const string TicketIdDescription =
        "Id of the ticket to (re)assign, from a create_ticket or search_tickets result.";
    private const string AssigneeDescription =
        "The agent's name from the roster (e.g. \"Alex Kim\"). Omit or leave empty to unassign.";
}
