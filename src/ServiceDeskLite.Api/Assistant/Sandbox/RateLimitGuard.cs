using Microsoft.Extensions.Options;

namespace ServiceDeskLite.Api.Assistant.Sandbox;

/// <summary>
/// Limits how many tool calls one owner may make per minute.
/// </summary>
/// <remarks>
/// <see cref="Check"/> only peeks and <see cref="Commit"/> takes the token, per the
/// <see cref="IToolGuard"/> contract. Two turns racing between the two can therefore each see the
/// last token and both proceed; the overshoot is bounded by the number of concurrent turns for
/// one owner and self-corrects as the bucket refills. Closing that window would mean holding a
/// lock across the whole guard chain, which is a worse trade for a safety net.
/// </remarks>
public sealed class RateLimitGuard : IToolGuard
{
    public const string BucketName = "tool-calls";

    private readonly TokenBucketRegistry _buckets;
    private readonly AgentSandboxOptions _options;
    private readonly ILogger<RateLimitGuard> _logger;

    public RateLimitGuard(
        TokenBucketRegistry buckets,
        IOptions<AgentSandboxOptions> options,
        ILogger<RateLimitGuard> logger)
    {
        _buckets = buckets ?? throw new ArgumentNullException(nameof(buckets));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ToolGuardResult Check(ToolInvocationContext context) =>
        _buckets.HasCapacity(context.Owner, BucketName, _options.ToolCallsPerMinute)
            ? ToolGuardResult.Allow()
            : ToolGuardResult.Deny(
                $"Tool calls are rate limited to {_options.ToolCallsPerMinute} per minute and the limit is reached. "
                + "Stop calling tools and tell the user to retry in a moment.");

    public void Commit(ToolInvocationContext context)
    {
        if (!_buckets.TryConsume(context.Owner, BucketName, _options.ToolCallsPerMinute))
            _logger.LogDebug("Tool-call bucket was drained between check and commit; allowing this call.");
    }
}
