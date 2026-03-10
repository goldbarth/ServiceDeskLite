namespace ServiceDeskLite.Application.Common;

/// <summary>
/// Abstraction over the system clock.
/// Rule: every handler or service that needs the current time must receive
/// an <see cref="IClock"/> via constructor injection — never call
/// <c>DateTimeOffset.UtcNow</c> directly in application or infrastructure code.
/// This keeps all time-dependent logic deterministic and fully testable.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
