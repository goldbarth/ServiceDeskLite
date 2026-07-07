namespace ServiceDeskLite.Api.Assistant;

public sealed partial class UpdateTicketTool
{
    private const string ToolDescription =
        """
        Update any existing ticket, e.g. to correct the due date, priority, title or description.
        Only pass the fields that should change; omitted fields stay unchanged. Requires the
        ticket id, obtained either from an earlier create_ticket result in this conversation or
        by resolving the user's description with search_tickets first. Do not guess an id.
        """;

    private const string TicketIdDescription =
        "Id of the ticket to update, from a create_ticket or search_tickets result.";
    private const string TitleDescription = "New title. Omit to keep the current one.";
    private const string DescriptionDescription = "New description. Omit to keep the current one.";
    private const string PriorityDescription = "New priority. Omit to keep the current one.";
    private const string DueAtDescription =
        "New due date (ISO 8601, include the user's UTC offset). Omit to keep the current one.";
}
