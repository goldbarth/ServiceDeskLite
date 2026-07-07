namespace ServiceDeskLite.Api.Assistant;

public sealed partial class FindSimilarTicketsTool
{
    private const string ToolDescription =
        """
        Semantic search over existing tickets. Use this before creating a ticket to check
        for duplicates or related work, or when the user asks whether an issue is already
        known. The query is matched by meaning, not keywords, so describe the problem in a
        full sentence.
        """;

    private const string QueryDescription =
        "The problem description to search for, e.g. \"printer on 3rd floor not printing\".";
    private static readonly string LimitDescription =
        $"Maximum number of matches to return (default {DefaultLimit}).";
}
