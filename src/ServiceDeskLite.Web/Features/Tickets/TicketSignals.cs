using MudBlazor;

using ServiceDeskLite.Contracts.V1.Tickets;

namespace ServiceDeskLite.Web.Features.Tickets;

/// <summary>
/// The one mapping from status and priority to label, icon, and token class. Every page
/// that renders either signal reads it from here; a glyph or wording that differs between
/// the queue, the board, and the detail page is a bug, not a design choice.
/// </summary>
public static class TicketSignals
{
    public static string Label(TicketStatus status)
        => status switch
        {
            TicketStatus.InProgress => "In Progress",
            _ => status.ToString()
        };

    public static string Label(TicketPriority priority)
        => priority.ToString();

    public static string Icon(TicketStatus status)
        => status switch
        {
            TicketStatus.New => Icons.Material.Outlined.Inbox,
            TicketStatus.Triaged => Icons.Material.Outlined.FactCheck,
            TicketStatus.InProgress => Icons.Material.Outlined.Autorenew,
            TicketStatus.Waiting => Icons.Material.Outlined.HourglassEmpty,
            TicketStatus.Resolved => Icons.Material.Outlined.CheckCircle,
            TicketStatus.Closed => Icons.Material.Outlined.Archive,
            _ => Icons.Material.Outlined.Circle
        };

    // Direction glyphs, not severity icons: under load the arrows sort a column at a
    // glance, and only Critical earns the exclamation mark.
    public static string Icon(TicketPriority priority)
        => priority switch
        {
            TicketPriority.Low => Icons.Material.Outlined.KeyboardArrowDown,
            TicketPriority.Medium => Icons.Material.Outlined.DragHandle,
            TicketPriority.High => Icons.Material.Outlined.KeyboardArrowUp,
            TicketPriority.Critical => Icons.Material.Outlined.PriorityHigh,
            _ => Icons.Material.Outlined.Circle
        };

    /// <summary>Matches the design-token names (<c>--sdl-status-new-*</c>), so the chip stylesheet is a mechanical projection of the token set.</summary>
    public static string Modifier(TicketStatus status)
        => status switch
        {
            TicketStatus.New => "status-new",
            TicketStatus.Triaged => "status-triaged",
            TicketStatus.InProgress => "status-inprogress",
            TicketStatus.Waiting => "status-waiting",
            TicketStatus.Resolved => "status-resolved",
            TicketStatus.Closed => "status-closed",
            _ => "status-new"
        };

    public static string Modifier(TicketPriority priority)
        => priority switch
        {
            TicketPriority.Low => "priority-low",
            TicketPriority.Medium => "priority-medium",
            TicketPriority.High => "priority-high",
            TicketPriority.Critical => "priority-critical",
            _ => "priority-low"
        };
}
