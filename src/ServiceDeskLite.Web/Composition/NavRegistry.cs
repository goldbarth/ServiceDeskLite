using MudBlazor;

using ServiceDeskLite.Web.Components.Layout;

namespace ServiceDeskLite.Web.Composition;

public static class NavRegistry
{
    public static readonly IReadOnlyList<NavItem> Items =
    [
        new("Dashboard", AppRoutes.Dashboard, Icons.Material.Filled.Dashboard, 50),
        new("Tickets", AppRoutes.Tickets, Icons.Material.Filled.ConfirmationNumber, 100),
        new("Board", AppRoutes.TicketBoard, Icons.Material.Filled.ViewKanban, 110),
        new("Assistant", AppRoutes.Assistant, Icons.Material.Filled.SmartToy, 120),
        new("AI Insights", AppRoutes.AiInsights, Icons.Material.Filled.Insights, 130),

        // Section (no link)
        new("Admin", null, Icons.Material.Filled.AdminPanelSettings, 200, IsSection: true),

        // Children under Admin (grouped by folder)
        new("Users", AppRoutes.Admin.Users, Icons.Material.Filled.Group, 210),
        new("Settings", AppRoutes.Admin.Settings, Icons.Material.Filled.Settings, 220),
    ];
}
