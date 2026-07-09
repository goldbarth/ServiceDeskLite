using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Infrastructure.Persistence.AssistantMetrics;

/// <summary>
/// Persists assistant metrics on their own <see cref="ServiceDeskLiteDbContext"/>, resolved
/// from a fresh scope per write.
/// </summary>
/// <remarks>
/// Two reasons, both load-bearing.
/// <para>
/// Sharing the request's context would make a metrics <c>SaveChanges</c> flush whatever that
/// context happens to be tracking. A command that failed after staging entities but before
/// saving would then get its partial write committed by a telemetry call — a corruption path
/// through code that is supposed to observe, not act.
/// </para>
/// <para>
/// Telemetry must never fail the thing it observes. Every write swallows its exception and
/// logs it: a dropped metric costs a dashboard row, while a thrown one costs the user's
/// chat turn.
/// </para>
/// </remarks>
public sealed class EfAssistantMetricsSink : IAssistantMetricsSink
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfAssistantMetricsSink> _logger;

    public EfAssistantMetricsSink(IServiceScopeFactory scopeFactory, ILogger<EfAssistantMetricsSink> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task RecordToolInvocationAsync(AssistantToolInvocation invocation, CancellationToken ct) =>
        SaveAsync(new AssistantToolInvocationRecord
        {
            Id = Guid.CreateVersion7(),
            ToolName = invocation.ToolName,
            Kind = invocation.Kind,
            IsError = invocation.IsError,
            Confidence = invocation.Confidence,
            MatchCount = invocation.MatchCount,
            SemanticAvailable = invocation.SemanticAvailable,
            OccurredAt = invocation.OccurredAt,
        }, ct);

    public Task RecordTokenUsageAsync(AssistantTokenUsage usage, CancellationToken ct) =>
        SaveAsync(new AssistantTokenUsageRecord
        {
            Id = Guid.CreateVersion7(),
            Model = usage.Model,
            InputTokens = usage.InputTokens,
            OutputTokens = usage.OutputTokens,
            OccurredAt = usage.OccurredAt,
        }, ct);

    private async Task SaveAsync(object record, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ServiceDeskLiteDbContext>();

            db.Add(record);
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // The client went away mid-turn. Losing its last metric is not worth a log line.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record assistant metric of type {Record}", record.GetType().Name);
        }
    }
}
