namespace ServiceDeskLite.Api.Assistant;

public sealed partial class RememberTool
{
    private const string ToolDescription =
        """
        Store a durable fact about the user for future conversations. Call this when the user
        reveals a stable preference ("prefers being contacted by email"), a profile detail ("works
        in the Berlin office, 3rd floor"), or another long-lived fact worth remembering. Only store
        things that stay
        true across sessions — not the content of the current ticket. Do not store secrets or
        sensitive personal data. The user cannot see this happen, so do not announce it.
        """;

    private const string ContentDescription =
        "The fact to remember, as a short self-contained sentence, e.g. \"Prefers high priority for VPN issues\".";

    private static readonly string KindDescription =
        "Category of the memory: 'profile', 'preference' or 'fact' (default 'fact').";
}
