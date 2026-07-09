using System.Diagnostics;

using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Api.Observability;

/// <summary>
/// Emits every recorded assistant signal to the metric instruments, then passes it to the
/// persisting sink underneath.
/// </summary>
/// <remarks>
/// A decorator rather than a second call site in <see cref="Assistant.AssistantChatService"/>:
/// the agent records a signal once, and both the AI dashboard and Prometheus see it. A new signal
/// cannot reach one and miss the other, which is exactly how the two would drift apart.
/// <para>
/// Metric recording is synchronous and allocation-light, and the inner sink already swallows its
/// own failures, so this adds no failure mode to the chat turn.
/// </para>
/// </remarks>
public sealed class MeterAssistantMetricsSink : IAssistantMetricsSink
{
    private readonly IAssistantMetricsSink _inner;
    private readonly AssistantInstrumentation _instruments;

    public MeterAssistantMetricsSink(IAssistantMetricsSink inner, AssistantInstrumentation instruments)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _instruments = instruments ?? throw new ArgumentNullException(nameof(instruments));
    }

    public Task RecordToolInvocationAsync(AssistantToolInvocation invocation, CancellationToken ct)
    {
        var tool = new KeyValuePair<string, object?>("tool", invocation.ToolName);
        var kind = new KeyValuePair<string, object?>("kind", invocation.Kind.ToString());
        var error = new KeyValuePair<string, object?>("error", invocation.IsError);

        _instruments.ToolCalls.Add(1, tool, kind, error);
        _instruments.ToolDuration.Record(invocation.Duration.TotalMilliseconds, tool, kind, error);

        // Retrieval confidence only, and only where it was measured. A routing or grounding score
        // is a different quantity that happens to share the range; recording it here would give
        // the histogram a meaning no query could rely on. Recording a zero for a keyword-only
        // retrieval would be just as wrong: the value was never observed.
        if (IsRetrieval(invocation.Kind) && invocation.Confidence is { } confidence)
            _instruments.RetrievalConfidence.Record(confidence, tool, kind);

        Activity.Current?.SetTag("tool.duration_ms", invocation.Duration.TotalMilliseconds);

        return _inner.RecordToolInvocationAsync(invocation, ct);
    }

    public Task RecordTokenUsageAsync(AssistantTokenUsage usage, CancellationToken ct)
    {
        var model = new KeyValuePair<string, object?>("model", usage.Model);

        _instruments.ModelTurns.Add(1, model);
        _instruments.Tokens.Add(usage.InputTokens, model, new("direction", "input"));
        _instruments.Tokens.Add(usage.OutputTokens, model, new("direction", "output"));

        return _inner.RecordTokenUsageAsync(usage, ct);
    }

    private static bool IsRetrieval(AssistantToolKind kind) =>
        kind is AssistantToolKind.Retrieval or AssistantToolKind.DuplicateCheck;
}
