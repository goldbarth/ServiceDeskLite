using Microsoft.AspNetCore.Components.Routing;

namespace ServiceDeskLite.Web.Composition;

public sealed record NavItem(
    string Title,
    string? Href,
    string Icon,
    int Order,
    NavLinkMatch Match = NavLinkMatch.All,
    bool IsSection = false);
