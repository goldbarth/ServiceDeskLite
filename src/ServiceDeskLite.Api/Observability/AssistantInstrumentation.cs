using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ServiceDeskLite.Api.Observability;

/// <summary>
/// The assistant's metric instruments and trace source.
/// </summary>
/// <remarks>
/// Built on <see cref="Meter"/> and <see cref="ActivitySource"/> from the BCL rather than a
/// Prometheus client type, so the instruments themselves carry no vendor. Which backend scrapes
/// or receives them is a composition-root decision (ADR-0036).
/// <para>
/// There is no error-rate instrument. An error rate is a ratio, and a ratio computed at record
/// time cannot be re-aggregated: summing two windows' rates is meaningless. The call counter
/// carries an <c>error</c> label instead, and the rate is derived at query time from the two
/// series, where it can be sliced by tool, by window, and by instance.
/// </para>
/// </remarks>
public sealed class AssistantInstrumentation : IDisposable
{
    public const string MeterName = "ServiceDeskLite.Assistant";
    public const string ActivitySourceName = "ServiceDeskLite.Assistant";

    // Named so the composition root can attach explicit histogram boundaries without a second copy
    // of the string drifting out of sync.
    public const string ToolDurationName = "servicedesklite.assistant.tool.duration";
    public const string RetrievalConfidenceName = "servicedesklite.assistant.retrieval.confidence";

    private readonly Meter _meter;

    public AssistantInstrumentation(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        ToolCalls = _meter.CreateCounter<long>(
            "servicedesklite.assistant.tool.calls",
            unit: "{call}",
            description: "Assistant tool invocations, labelled by tool, kind and whether they failed.");

        ToolDuration = _meter.CreateHistogram<double>(
            ToolDurationName,
            unit: "ms",
            description: "Wall-clock time one assistant tool invocation took, including retries.");

        RetrievalConfidence = _meter.CreateHistogram<double>(
            RetrievalConfidenceName,
            unit: "1",
            description: "Top-match relevance reported by a retrieval tool, between 0 and 1. "
                + "Only recorded where it was measured; a keyword-only retrieval records nothing.");

        ModelTurns = _meter.CreateCounter<long>(
            "servicedesklite.assistant.model.turns",
            unit: "{turn}",
            description: "Anthropic round trips, across chat turns and ticket summaries.");

        Tokens = _meter.CreateCounter<long>(
            "servicedesklite.assistant.tokens",
            unit: "{token}",
            description: "Tokens billed, labelled by model and direction (input or output).");
    }

    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);

    public Counter<long> ToolCalls { get; }
    public Histogram<double> ToolDuration { get; }
    public Histogram<double> RetrievalConfidence { get; }
    public Counter<long> ModelTurns { get; }
    public Counter<long> Tokens { get; }

    public void Dispose() => _meter.Dispose();
}
