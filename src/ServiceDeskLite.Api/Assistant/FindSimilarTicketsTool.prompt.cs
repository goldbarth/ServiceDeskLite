namespace ServiceDeskLite.Api.Assistant;

public sealed partial class FindSimilarTicketsTool
{
    private const string ToolDescription =
        """
        Hybrid search over existing tickets. Use this before creating a ticket to check
        for duplicates or related work, or when the user asks whether an issue is already
        known. It blends meaning-based (semantic) and keyword matching and ranks the
        merged results, so describe the problem in a full sentence. Optionally narrow the
        search by status or priority. Each result carries a relevance score and shows
        which signals matched it; when semantic search is unavailable the tool falls back
        to keyword-only and labels the results as weaker evidence.
        """;

    private const string QueryDescription =
        "The problem description to search for, e.g. \"printer on 3rd floor not printing\".";

    private static readonly string LimitDescription =
        $"Maximum number of matches to return (default {DefaultLimit}).";

    private const string StatusesDescription =
        "Optional filter: only return tickets in these workflow statuses (e.g. [\"New\", \"InProgress\"]).";

    private const string PrioritiesDescription =
        "Optional filter: only return tickets with these priorities (e.g. [\"High\", \"Critical\"]).";
}
