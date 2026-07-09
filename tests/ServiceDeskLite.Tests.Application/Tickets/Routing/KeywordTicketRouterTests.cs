using FluentAssertions;

using ServiceDeskLite.Application.Tickets.Routing;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Application.Tickets.Routing;

public sealed class KeywordTicketRouterTests
{
    private readonly KeywordTicketRouter _router = new();

    [Fact]
    public void Route_NetworkOutage_ClassifiesHighConfidenceWithOwnerAndTriage()
    {
        var d = _router.Route("VPN is down", "The VPN is down for everyone, nobody can connect.");

        d.Category.Should().Be(TicketCategory.Network);
        d.Priority.Should().Be(TicketPriority.Critical);
        d.SuggestedAssignee.Should().Be("Alex Kim");
        d.SuggestedStatus.Should().Be(TicketStatus.Triaged);
        // Category + priority both matched a keyword: base 0.3 + 0.35 + 0.25.
        d.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void Route_PasswordReset_ClassifiesAccessWithDefaultPriority()
    {
        var d = _router.Route("Password help", "I need to reset my password, I am locked out.");

        d.Category.Should().Be(TicketCategory.Access);
        d.Priority.Should().Be(TicketPriority.Medium);
        d.SuggestedAssignee.Should().Be("Jordan Lee");
        // Category matched, priority defaulted: 0.3 + 0.35.
        d.Confidence.Should().Be(0.65);
    }

    [Fact]
    public void Route_PrinterHowTo_ClassifiesHardwareLowPriority()
    {
        var d = _router.Route("Printer setup", "How do I install the printer driver on my laptop?");

        d.Category.Should().Be(TicketCategory.Hardware);
        d.Priority.Should().Be(TicketPriority.Low);
        d.SuggestedAssignee.Should().Be("Sam Rivera");
        d.Confidence.Should().Be(0.9);
    }

    [Fact]
    public void Route_UnrecognizedContent_FallsBackToOtherWithLowConfidence()
    {
        var d = _router.Route("Hello", "I have a thing I want to talk about.");

        d.Category.Should().Be(TicketCategory.Other);
        d.Priority.Should().Be(TicketPriority.Medium);
        d.SuggestedAssignee.Should().BeNull();
        // Nothing matched: stays at the base, below the apply threshold.
        d.Confidence.Should().Be(0.3);
        d.Confidence.Should().BeLessThan(RouteTicketHandler.ApplyThreshold);
    }

    [Fact]
    public void Route_IsCaseInsensitive()
    {
        var lower = _router.Route("vpn down", "vpn down");
        var upper = _router.Route("VPN DOWN", "VPN DOWN");

        upper.Category.Should().Be(lower.Category);
        upper.Priority.Should().Be(lower.Priority);
        upper.Confidence.Should().Be(lower.Confidence);
    }

    [Fact]
    public void Route_RationaleNamesTheMatchedSignals()
    {
        var d = _router.Route("VPN down", "The vpn is down.");

        d.Rationale.Should().Contain("Network");
        d.Rationale.Should().Contain("Critical");
    }
}
