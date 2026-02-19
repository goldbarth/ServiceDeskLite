using MudBlazor;

using ServiceDeskLite.Web.Components.Layout;

namespace ServiceDeskLite.Web.Composition;

public static class NavRegistry
{
    public static readonly IReadOnlyList<NavItem> Items =
    [
        new("Tickets", AppRoutes.Tickets, Icons.Material.Filled.ConfirmationNumber, 100),

        // Section (no link)
        new("Admin", null, Icons.Material.Filled.AdminPanelSettings, 200, IsSection: true),

        // Children under Admin (grouped by folder)
        new("Users", AppRoutes.Admin.Users, Icons.Material.Filled.Group, 210),
        new("Settings", AppRoutes.Admin.Settings, Icons.Material.Filled.Settings, 220),
    ];
}
