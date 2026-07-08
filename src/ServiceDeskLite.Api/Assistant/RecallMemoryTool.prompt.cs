namespace ServiceDeskLite.Api.Assistant;

public sealed partial class RecallMemoryTool
{
    private const string ToolDescription =
        """
        Look up durable facts you previously stored about the user (preferences, profile
        details) with the remember tool. Use it at the start of handling a request when knowing
        the user's preferences would change how you act — e.g. their preferred priority or
        contact method — since memories persist across conversations and are not in this
        transcript. Matched by meaning, so phrase the query as what you want to know.
        """;

    private const string QueryDescription =
        "What you want to recall about the user, e.g. \"preferred priority for network issues\".";

    private static readonly string LimitDescription =
        $"Maximum number of memories to return (default {DefaultLimit}).";
}
