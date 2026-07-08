using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Owner accessor for the demo: there is no authentication, so all conversations
/// and memories belong to one implicit user. Real auth replaces only this class
/// with one that reads the owner from the security principal — the seam is
/// <see cref="ICurrentUser"/>, nothing downstream changes.
/// </summary>
internal sealed class DemoCurrentUser : ICurrentUser
{
    private static readonly OwnerId DemoOwner = new(new Guid("00000000-0000-0000-0000-000000000001"));

    public OwnerId Owner => DemoOwner;
}
