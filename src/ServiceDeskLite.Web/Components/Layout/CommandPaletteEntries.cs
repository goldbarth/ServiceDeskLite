using MudBlazor;

using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Composition;

namespace ServiceDeskLite.Web.Components.Layout;

public enum PaletteEntryKind
{
    Navigate,
    Ticket
}

/// <summary>One selectable row in the command palette, reduced to what the view needs.</summary>
public sealed record PaletteEntry(
    PaletteEntryKind Kind,
    string Label,
    string? Detail,
    string Icon,
    string Href);

/// <summary>
/// Builds the palette's flat result list from the two sources the issue allows: navigation
/// targets drawn from <see cref="NavRegistry"/> (never a second hard-coded list) and ticket
/// hits from the search endpoint. Navigation entries come first so an empty query still offers
/// the whole app map; ticket hits follow once the user types.
/// </summary>
public static class CommandPaletteEntries
{
    public static IReadOnlyList<PaletteEntry> Build(
        string? query,
        IReadOnlyList<NavItem> navItems,
        IReadOnlyList<TicketListItemResponse> tickets)
    {
        var trimmed = query?.Trim() ?? string.Empty;

        var navigation = navItems
            .Where(item => item is { Href: not null, IsSection: false })
            .Where(item => trimmed.Length == 0
                || item.Title.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .Select(item => new PaletteEntry(
                PaletteEntryKind.Navigate, item.Title, "Navigate", item.Icon, item.Href!));

        var ticketHits = tickets
            .Select(ticket => new PaletteEntry(
                PaletteEntryKind.Ticket,
                ticket.Title,
                ticket.DisplayRef,
                Icons.Material.Filled.ConfirmationNumber,
                $"{AppRoutes.Tickets}/{ticket.Id}"));

        return [.. navigation, .. ticketHits];
    }
}
