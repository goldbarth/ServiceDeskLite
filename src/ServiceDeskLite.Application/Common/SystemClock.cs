namespace ServiceDeskLite.Application.Common;

/// <summary>
/// Production implementation of <see cref="IClock"/> that delegates to
/// <see cref="DateTimeOffset.UtcNow"/>. Register as Singleton.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
