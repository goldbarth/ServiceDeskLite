namespace ServiceDeskLite.Infrastructure.Embeddings;

/// <summary>
/// Anthropic has no embeddings endpoint; Voyage AI is their recommended
/// embedding provider. Unlike Anthropic:ApiKey (fail-fast, assistant is a
/// core feature), the Voyage key defaults to a placeholder so the app runs
/// with semantic search disabled — RAG is an optional enhancement.
/// </summary>
public sealed class VoyageOptions
{
    public const string SectionName = "Voyage";

    /// <summary>Sentinel meaning "RAG intentionally disabled"; mirrors the assistant placeholder in docker-compose.</summary>
    public const string DisabledPlaceholder = "placeholder-rag-disabled";

    /// <summary>Set via user-secrets (dev) or environment variable, never in appsettings.json.</summary>
    public string ApiKey { get; init; } = DisabledPlaceholder;

    public string Model { get; init; } = "voyage-3.5";

    /// <summary>Must match the vector column width (TicketEmbeddingConfiguration.Dimensions).</summary>
    public int Dimensions { get; init; } = 1024;

    public string BaseUrl { get; init; } = "https://api.voyageai.com/v1/";

    /// <summary>Max tickets embedded per API call; Voyage accepts far more, kept small to bound request size.</summary>
    public int BatchSize { get; init; } = 64;

    /// <summary>Delay between embedding worker polls.</summary>
    public int PollSeconds { get; init; } = 15;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && ApiKey != DisabledPlaceholder;
}
