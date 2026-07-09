using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

/// <summary>
/// Append-only metrics in process memory. Unlike the EF sink there is no transaction to
/// contaminate and nothing to fail, so the records are simply stored — the honesty about
/// what the numbers mean (per-process, lost on restart) belongs to the runbook, not here.
/// </summary>
internal sealed class InMemoryAssistantMetricsSink : IAssistantMetricsSink
{
    private readonly InMemoryStore _store;

    public InMemoryAssistantMetricsSink(InMemoryStore store)
        => _store = store;

    public Task RecordToolInvocationAsync(AssistantToolInvocation invocation, CancellationToken ct)
    {
        _store.AppendToolInvocation(invocation);
        return Task.CompletedTask;
    }

    public Task RecordTokenUsageAsync(AssistantTokenUsage usage, CancellationToken ct)
    {
        _store.AppendTokenUsage(usage);
        return Task.CompletedTask;
    }
}
