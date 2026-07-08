namespace ServiceDeskLite.Application.Abstractions.Assistant;

/// <summary>
/// Resolves the owner on whose behalf the assistant acts. This is the single
/// seam where real authentication will later plug in: today the implementation
/// returns a constant demo owner; an authenticated deployment swaps it for one
/// that reads the owner from the security principal — no other code changes.
/// </summary>
public interface ICurrentUser
{
    OwnerId Owner { get; }
}
