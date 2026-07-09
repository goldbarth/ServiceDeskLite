using Microsoft.Extensions.Options;

using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Api.Assistant.Sandbox;

/// <summary>
/// Limits model round trips per owner, across every path that calls the Anthropic API.
/// </summary>
/// <remarks>
/// The tool-call limit bounds what the assistant does; this bounds what it costs. Both chat turns
/// and ticket summaries consume from the same bucket, because to the account they are the same
/// spend — ADR-0033 left summaries unlimited, and this closes that gap.
/// </remarks>
public sealed class ModelTurnLimiter
{
    public const string BucketName = "model-turns";

    private readonly TokenBucketRegistry _buckets;
    private readonly AgentSandboxOptions _options;

    public ModelTurnLimiter(TokenBucketRegistry buckets, IOptions<AgentSandboxOptions> options)
    {
        _buckets = buckets ?? throw new ArgumentNullException(nameof(buckets));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Takes one model round trip from <paramref name="owner"/>'s budget.</summary>
    public bool TryConsume(OwnerId owner)
        => _buckets.TryConsume(owner, BucketName, _options.ModelTurnsPerMinute);

    /// <summary>What the client is told when the budget is exhausted. Never mentions the model.</summary>
    public string LimitMessage =>
        $"You have reached the limit of {_options.ModelTurnsPerMinute} assistant requests per minute. "
        + "Please try again in a moment.";
}
