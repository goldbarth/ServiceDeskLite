namespace ServiceDeskLite.Infrastructure.Persistence.AssistantMetrics;

/// <summary>
/// Token cost of one model turn, as reported by the Anthropic API. Append-only.
/// </summary>
public sealed class AssistantTokenUsageRecord
{
    public Guid Id { get; set; }

    public string Model { get; set; } = string.Empty;

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
