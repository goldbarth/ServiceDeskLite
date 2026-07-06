namespace ServiceDeskLite.Api.Assistant;

public sealed partial class UpdateTicketTool
{
    private const string ToolDescription =
        """
        Update an existing ticket, e.g. to correct the due date, priority, title or description
        after the user clarifies. Only pass the fields that should change. Requires the ticket id
        from an earlier create_ticket result in this conversation.
        """;

    private const string TicketIdDescription = "Id of the ticket to update.";
    private const string TitleDescription = "New title. Omit to keep the current one.";
    private const string DescriptionDescription = "New description. Omit to keep the current one.";
    private const string PriorityDescription = "New priority. Omit to keep the current one.";
    private const string DueAtDescription =
        "New due date (ISO 8601, include the user's UTC offset). Omit to keep the current one.";
}
