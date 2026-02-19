namespace ServiceDeskLite.Web.Components.Layout;

public static class AppRoutes
{
    public const string Tickets = "/tickets";
    public const string TicketDetails = "/tickets/{id:guid}";

    public static class Admin
    {
        public const string Users = "/admin/users";
        public const string Settings = "/admin/settings";
    }
}
