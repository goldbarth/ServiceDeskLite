namespace ServiceDeskLite.Api.Assistant;

public sealed partial class CreateTicketTool
{
    private const string ToolDescription =
        """
        Create a support ticket from the user's description. Use this when the user
        reports a problem or requests work. Derive a concise title and pick a priority
        matching the impact described.
        """;

    private const string TitleDescription = "Short summary of the issue (max ~80 chars).";
    private const string DescriptionDescription = "Full problem description, based on what the user reported.";
    private const string PriorityDescription = "Impact-based priority.";
    private const string DueAtDescription = "Optional due date (ISO 8601), only if the user mentioned a deadline.";
}
