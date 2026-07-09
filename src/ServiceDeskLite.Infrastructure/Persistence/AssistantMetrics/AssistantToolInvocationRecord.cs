using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Infrastructure.Persistence.AssistantMetrics;

/// <summary>
/// One persisted tool invocation. Append-only: nothing updates or deletes these rows,
/// and no behaviour reads them except the AI dashboard.
/// </summary>
public sealed class AssistantToolInvocationRecord
{
    public Guid Id { get; set; }

    public string ToolName { get; set; } = string.Empty;

    public AssistantToolKind Kind { get; set; }

    public bool IsError { get; set; }

    /// <summary>Top-match relevance, when the tool reports one. Null is "not measured", not zero.</summary>
    public double? Confidence { get; set; }

    /// <summary>Results returned by a retrieval tool; null for tools that retrieve nothing.</summary>
    public int? MatchCount { get; set; }

    /// <summary>Whether a retrieval's semantic half ran; null for non-retrieval tools.</summary>
    public bool? SemanticAvailable { get; set; }

    /// <summary>
    /// Wall-clock milliseconds the invocation took, retries included. Stored rather than left to
    /// the Prometheus histogram, because a scrape window cannot answer what latency looked like
    /// last Tuesday.
    /// </summary>
    public double DurationMs { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
