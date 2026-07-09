namespace ServiceDeskLite.Api.Assistant;

public sealed partial class RouteTicketTool
{
    private const string ToolDescription =
        """
        Auto-triage a ticket from its content: derive a category, priority, assignee, and
        workflow status. Call this right after creating a ticket to route it. When the
        router is confident the changes are applied automatically (and audited); when it is
        uncertain the tool returns a suggestion WITHOUT applying it — relay that to the user
        and confirm, or set the fields yourself, rather than committing an uncertain triage.
        Report what was routed (or suggested) briefly.
        """;

    private const string TicketIdDescription =
        "The UUID of the ticket to route, from a previous create_ticket or search_tickets result.";
}
