namespace ServiceDeskLite.Api.Assistant;

public sealed partial class ChangeTicketStatusTool
{
    private const string ToolDescription =
        """
        Move an existing ticket to a new workflow status, e.g. "mark it as in progress" or
        "close the printer ticket". The workflow state machine is authoritative: only certain
        transitions are allowed from each status. If a transition is rejected, relay the reason
        to the user rather than retrying blindly. Requires the ticket id, from an earlier
        create_ticket result or resolved from the user's description with search_tickets first.
        """;

    private const string TicketIdDescription =
        "Id of the ticket whose status to change, from a create_ticket or search_tickets result.";
    private const string StatusDescription =
        "The target workflow status to move the ticket to.";
}
