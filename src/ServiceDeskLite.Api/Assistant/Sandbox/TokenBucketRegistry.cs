using System.Collections.Concurrent;

using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Api.Assistant.Sandbox;

/// <summary>
/// Per-owner token buckets, held in process memory.
/// </summary>
/// <remarks>
/// Deliberately not persisted (ADR-0035). A limit measured per minute is meaningless across a
/// restart, and a database round trip in front of every tool call would make the limiter a
/// dependency of the thing it protects. The cost is that the limit is per instance: a
/// multi-instance deployment would need a shared store, and that is a different decision.
/// </remarks>
public sealed class TokenBucketRegistry
{
    private readonly ConcurrentDictionary<(OwnerId Owner, string Name), TokenBucket> _buckets = new();
    private readonly IClock _clock;

    public TokenBucketRegistry(IClock clock)
        => _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>Whether a token is available, without taking it.</summary>
    public bool HasCapacity(OwnerId owner, string name, int capacityPerMinute)
        => Bucket(owner, name, capacityPerMinute).HasCapacity(_clock.UtcNow);

    /// <summary>Takes a token if one is available.</summary>
    public bool TryConsume(OwnerId owner, string name, int capacityPerMinute)
        => Bucket(owner, name, capacityPerMinute).TryConsume(_clock.UtcNow);

    private TokenBucket Bucket(OwnerId owner, string name, int capacityPerMinute)
        => _buckets.GetOrAdd((owner, name), _ => new TokenBucket(capacityPerMinute, _clock.UtcNow));
}

/// <summary>
/// A bucket that refills continuously at <c>capacity</c> tokens per minute and never holds more
/// than <c>capacity</c>. Continuous refill rather than a fixed window, so a caller cannot spend a
/// full window's allowance at its end and another immediately at the start of the next.
/// </summary>
internal sealed class TokenBucket
{
    private readonly object _sync = new();
    private readonly double _capacity;
    private readonly double _tokensPerSecond;

    private double _tokens;
    private DateTimeOffset _updatedAt;

    public TokenBucket(int capacityPerMinute, DateTimeOffset now)
    {
        _capacity = capacityPerMinute;
        _tokensPerSecond = capacityPerMinute / 60d;
        _tokens = capacityPerMinute;
        _updatedAt = now;
    }

    public bool HasCapacity(DateTimeOffset now)
    {
        lock (_sync)
        {
            Refill(now);
            return _tokens >= 1d;
        }
    }

    public bool TryConsume(DateTimeOffset now)
    {
        lock (_sync)
        {
            Refill(now);
            if (_tokens < 1d)
                return false;

            _tokens -= 1d;
            return true;
        }
    }

    private void Refill(DateTimeOffset now)
    {
        var elapsed = (now - _updatedAt).TotalSeconds;
        if (elapsed <= 0)
            return;

        _tokens = Math.Min(_capacity, _tokens + (elapsed * _tokensPerSecond));
        _updatedAt = now;
    }
}
