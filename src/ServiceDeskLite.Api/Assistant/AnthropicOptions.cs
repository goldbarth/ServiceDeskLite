namespace ServiceDeskLite.Api.Assistant;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>Set via user-secrets (dev) or environment variable, never in appsettings.json.</summary>
    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "claude-opus-4-8";

    public int MaxTokens { get; init; } = 8192;

    /// <summary>
    /// Upper bound on model→tool→model round trips per request. The
    /// duplicate-check flow needs two (find_similar_tickets, then
    /// create_ticket) plus headroom for one correction round.
    /// </summary>
    public int MaxToolIterations { get; init; } = 4;

    /// <summary>
    /// IANA/Windows timezone the model uses to resolve relative dates ("by Friday
    /// morning") and to express due dates, so ticket times match what users see.
    /// </summary>
    public string UserTimeZone { get; init; } = "Europe/Berlin";
}
