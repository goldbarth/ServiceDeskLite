using System.Diagnostics.Metrics;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

using ServiceDeskLite.Api.Observability;
using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Tests.Api.Observability;

/// <summary>
/// The decorator is the only thing standing between a recorded signal and Prometheus. What it
/// emits, and just as importantly what it declines to emit, is the contract a dashboard query is
/// written against.
/// </summary>
public sealed class MeterAssistantMetricsSinkTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
    private readonly AssistantInstrumentation _instruments;
    private readonly RecordingSink _inner = new();
    private readonly MeterAssistantMetricsSink _sink;

    public MeterAssistantMetricsSinkTests()
    {
        _instruments = new AssistantInstrumentation(_services.GetRequiredService<IMeterFactory>());
        _sink = new MeterAssistantMetricsSink(_inner, _instruments);
    }

    private static AssistantToolInvocation Invocation(
        string tool = "search_tickets",
        AssistantToolKind kind = AssistantToolKind.Retrieval,
        bool isError = false,
        double? confidence = null,
        double durationMs = 42)
        => new(tool, kind, isError, confidence, null, null, TimeSpan.FromMilliseconds(durationMs), At);

    [Fact]
    public async Task A_tool_call_is_counted_and_timed_with_tool_kind_and_error_labels()
    {
        using var calls = new MetricCollector<long>(_instruments.ToolCalls);
        using var duration = new MetricCollector<double>(_instruments.ToolDuration);

        await _sink.RecordToolInvocationAsync(
            Invocation(tool: "create_ticket", kind: AssistantToolKind.Action, isError: true, durationMs: 123),
            CancellationToken.None);

        var call = calls.GetMeasurementSnapshot().Should().ContainSingle().Subject;
        call.Value.Should().Be(1);
        call.Tags["tool"].Should().Be("create_ticket");
        call.Tags["kind"].Should().Be("Action");
        call.Tags["error"].Should().Be(true);

        duration.GetMeasurementSnapshot().Should().ContainSingle()
            .Which.Value.Should().Be(123);
    }

    [Fact]
    public async Task Confidence_is_recorded_only_where_it_was_measured()
    {
        using var confidence = new MetricCollector<double>(_instruments.RetrievalConfidence);

        await _sink.RecordToolInvocationAsync(Invocation(confidence: 0.8), CancellationToken.None);
        await _sink.RecordToolInvocationAsync(Invocation(confidence: null), CancellationToken.None);

        confidence.GetMeasurementSnapshot().Should().ContainSingle(
            "a keyword-only retrieval reports no score, and recording a zero would drag the histogram down");
        confidence.GetMeasurementSnapshot()[0].Value.Should().Be(0.8);
    }

    [Fact]
    public async Task A_non_retrieval_confidence_is_kept_out_of_the_retrieval_histogram()
    {
        using var confidence = new MetricCollector<double>(_instruments.RetrievalConfidence);

        // route_ticket reports a routing confidence, not top-match relevance. Averaging the two
        // together in one histogram would give a number no query could trust; the AI dashboard's
        // aggregation already excludes it, and the meter must make the same cut.
        await _sink.RecordToolInvocationAsync(
            Invocation(tool: "route_ticket", kind: AssistantToolKind.Action, confidence: 0.9),
            CancellationToken.None);

        confidence.GetMeasurementSnapshot().Should().BeEmpty();
    }

    [Fact]
    public async Task Token_usage_splits_into_input_and_output_series()
    {
        using var tokens = new MetricCollector<long>(_instruments.Tokens);
        using var turns = new MetricCollector<long>(_instruments.ModelTurns);

        await _sink.RecordTokenUsageAsync(new AssistantTokenUsage("claude-opus-4-8", 900, 100, At), default);

        turns.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(1);

        var measurements = tokens.GetMeasurementSnapshot();
        measurements.Should().HaveCount(2);
        measurements.Single(m => Equals(m.Tags["direction"], "input")).Value.Should().Be(900);
        measurements.Single(m => Equals(m.Tags["direction"], "output")).Value.Should().Be(100);
        measurements.Should().OnlyContain(m => Equals(m.Tags["model"], "claude-opus-4-8"));
    }

    [Fact]
    public async Task Everything_recorded_still_reaches_the_persisting_sink()
    {
        await _sink.RecordToolInvocationAsync(Invocation(), CancellationToken.None);
        await _sink.RecordTokenUsageAsync(new AssistantTokenUsage("claude-opus-4-8", 1, 2, At), default);

        _inner.Invocations.Should().ContainSingle();
        _inner.Usages.Should().ContainSingle("metrics must never replace the dashboard's own record");
    }

    public void Dispose()
    {
        _instruments.Dispose();
        _services.Dispose();
    }

    private sealed class RecordingSink : IAssistantMetricsSink
    {
        public List<AssistantToolInvocation> Invocations { get; } = [];
        public List<AssistantTokenUsage> Usages { get; } = [];

        public Task RecordToolInvocationAsync(AssistantToolInvocation invocation, CancellationToken ct)
        {
            Invocations.Add(invocation);
            return Task.CompletedTask;
        }

        public Task RecordTokenUsageAsync(AssistantTokenUsage usage, CancellationToken ct)
        {
            Usages.Add(usage);
            return Task.CompletedTask;
        }
    }
}
