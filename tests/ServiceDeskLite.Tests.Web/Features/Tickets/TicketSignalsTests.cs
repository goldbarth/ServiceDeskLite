using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Features.Tickets;
using ServiceDeskLite.Web.Theme;

namespace ServiceDeskLite.Tests.Web.Features.Tickets;

/// <summary>
/// TicketSignals is the one mapping behind every status and priority chip. These tests
/// guard the seam a new enum member slips through: it compiles fine, and only the UI
/// shows a blank icon or a chip without colours.
/// </summary>
public sealed class TicketSignalsTests
{
    [Fact]
    public void EveryStatusHasLabelIconAndModifier()
    {
        foreach (var status in Enum.GetValues<TicketStatus>())
        {
            TicketSignals.Label(status).Should().NotBeNullOrWhiteSpace();
            TicketSignals.Icon(status).Should().NotBeNullOrWhiteSpace();
            TicketSignals.Modifier(status).Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void EveryPriorityHasLabelIconAndModifier()
    {
        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            TicketSignals.Label(priority).Should().NotBeNullOrWhiteSpace();
            TicketSignals.Icon(priority).Should().NotBeNullOrWhiteSpace();
            TicketSignals.Modifier(priority).Should().NotBeNullOrWhiteSpace();
        }
    }

    // The modifier is the bridge between the chip stylesheet and the token set; a
    // modifier without matching tokens renders an uncoloured chip in both themes.
    [Fact]
    public void EveryModifierHasDesignTokens()
    {
        var modifiers = Enum.GetValues<TicketStatus>().Select(TicketSignals.Modifier)
            .Concat(Enum.GetValues<TicketPriority>().Select(TicketSignals.Modifier))
            .Distinct();

        foreach (var modifier in modifiers)
        {
            DesignTokens.RootCss.Should().Contain($"--sdl-{modifier}-fg:")
                .And.Contain($"--sdl-{modifier}-bg:")
                .And.Contain($"--sdl-{modifier}-border:");
        }
    }

    [Fact]
    public void PrioritiesCarryDistinctIcons()
    {
        var icons = Enum.GetValues<TicketPriority>().Select(TicketSignals.Icon).ToArray();
        icons.Should().OnlyHaveUniqueItems("an icon shared by two priorities defeats scanning a column");
    }
}
