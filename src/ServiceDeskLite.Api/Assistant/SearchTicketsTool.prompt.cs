namespace ServiceDeskLite.Api.Assistant;

public sealed partial class SearchTicketsTool
{
    private const string ToolDescription =
        """
        Find existing tickets by structured filter and optional free text, e.g. "show open
        tickets assigned to Alex" or "any high-priority tickets about login?". Use this when
        the user wants to list, look up, or act on tickets that already exist. Results are a
        compact list (id, title, status, assignee, priority) you can summarise; use a ticket's
        id to update it.

        This is NOT semantic duplicate detection — for checking whether a similar ticket
        already exists before creating a new one, use find_similar_tickets instead.
        """;

    private const string QueryDescription =
        "Optional free-text filter matched against ticket title and description, e.g. \"login\".";
    private const string StatusDescription =
        "Optional status filter. Returns tickets in any of the given statuses.";
    private const string PriorityDescription =
        "Optional priority filter. Returns tickets with any of the given priorities.";
    private const string AssigneeDescription =
        "Optional assignee name filter, e.g. \"Alex\".";
    private const string SortByDescription =
        "Optional field to sort by (default CreatedAt).";
    private const string SortDirectionDescription =
        "Optional sort direction (default Desc).";
    private static readonly string LimitDescription =
        $"Maximum number of tickets to return (1-{MaxLimit}, default {DefaultLimit}).";
}
