using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Components.Layout;
using ServiceDeskLite.Web.Composition;

namespace ServiceDeskLite.Tests.Web.Features.Layout;

public class CommandPaletteEntriesTests
{
    [Fact]
    public void EmptyQuery_ReturnsEveryNavigableItem_AndNoTickets()
    {
        var entries = CommandPaletteEntries.Build(string.Empty, NavRegistry.Items, []);

        entries.Should().OnlyContain(e => e.Kind == PaletteEntryKind.Navigate);
        // Exactly the NavRegistry items that are real links (Href set, not a section header).
        entries.Should().HaveCount(NavRegistry.Items.Count(i => i.Href is not null && !i.IsSection));
    }

    [Fact]
    public void SectionHeaders_AreNeverListed()
    {
        var entries = CommandPaletteEntries.Build(null, NavRegistry.Items, []);

        // "Admin" is a section (no Href); it must not appear as a navigation target.
        entries.Should().NotContain(e => e.Label == "Admin");
    }

    [Fact]
    public void Query_FiltersNavigationByTitle_CaseInsensitively()
    {
        var entries = CommandPaletteEntries.Build("ASSIST", NavRegistry.Items, []);

        entries.Should().ContainSingle()
            .Which.Label.Should().Be("Assistant");
    }

    [Fact]
    public void Query_MatchesAnySubstringOfTheTitle()
    {
        // "board" is a substring of both "Dashboard" and "Board".
        var entries = CommandPaletteEntries.Build("board", NavRegistry.Items, []);

        entries.Select(e => e.Label).Should().BeEquivalentTo("Dashboard", "Board");
    }

    [Fact]
    public void TicketHits_FollowNavigation_WithRefAndDetailRoute()
    {
        var id = Guid.NewGuid();
        var ticket = MakeTicket(id, "Printer offline", "#55289A");

        var entries = CommandPaletteEntries.Build("print", NavRegistry.Items, [ticket]);

        var ticketEntry = entries.Should().ContainSingle(e => e.Kind == PaletteEntryKind.Ticket).Subject;
        ticketEntry.Label.Should().Be("Printer offline");
        ticketEntry.Detail.Should().Be("#55289A");
        ticketEntry.Href.Should().Be($"/tickets/{id}");

        // Navigation entries are ordered before ticket hits.
        entries[^1].Kind.Should().Be(PaletteEntryKind.Ticket);
    }

    private static TicketListItemResponse MakeTicket(Guid id, string title, string displayRef) =>
        new(id, title, TicketPriority.Low, TicketCategory.Uncategorized, TicketStatus.New,
            DateTimeOffset.UtcNow, null, null, [], false, displayRef);
}
