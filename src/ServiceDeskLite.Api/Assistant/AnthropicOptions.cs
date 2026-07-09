namespace ServiceDeskLite.Api.Assistant;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>Set via user-secrets (dev) or environment variable, never in appsettings.json.</summary>
    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "claude-opus-4-8";

    public int MaxTokens { get; init; } = 8192;

    /// <summary>
    /// Output budget for a ticket summary. Far below <see cref="MaxTokens"/>: the four
    /// sections are deliberately short, and a low ceiling keeps a runaway generation from
    /// filling the panel with prose an agent will not read.
    /// </summary>
    public int SummaryMaxTokens { get; init; } = 2048;

    /// <summary>
    /// Upper bound on model→tool→model round trips per request. A full autonomous
    /// chain (find_similar_tickets → create_ticket → assign_ticket) needs three,
    /// plus headroom for a correction/retry round; kept configurable so longer
    /// sequences can raise it.
    /// </summary>
    public int MaxToolIterations { get; init; } = 6;

    /// <summary>Max transient-failure retries per tool call before the failure is surfaced to the model.</summary>
    public int MaxToolRetries { get; init; } = 2;

    /// <summary>Base delay for the tool retry's exponential backoff (base × 2^attempt).</summary>
    public int ToolRetryBaseDelayMs { get; init; } = 200;

    /// <summary>
    /// IANA/Windows timezone the model uses to resolve relative dates ("by Friday
    /// morning") and to express due dates, so ticket times match what users see.
    /// </summary>
    public string UserTimeZone { get; init; } = "Europe/Berlin";
}
