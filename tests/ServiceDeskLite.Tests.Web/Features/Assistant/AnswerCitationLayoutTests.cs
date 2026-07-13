using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Assistant;
using ServiceDeskLite.Web.Features.Assistant.Pages;

namespace ServiceDeskLite.Tests.Web.Features.Assistant;

public class AnswerCitationLayoutTests
{
    private static AssistantCitation Citation(string title, string heading = "Symptoms") =>
        new(title, "FAQ", heading, "Snippet text.", 0.9);

    [Fact]
    public void NoCitations_ReturnsSingleTextRun()
    {
        var layout = AnswerCitationLayout.Build("Just an answer.", []);

        layout.Runs.Should().ContainSingle()
            .Which.Should().BeOfType<AnswerTextRun>()
            .Which.Text.Should().Be("Just an answer.");
        layout.UnanchoredCitations.Should().BeEmpty();
    }

    [Fact]
    public void TitleMentioned_AnchorsBadgeAfterClosingQuote()
    {
        var text = "Try the steps per \"VPN Connection Troubleshooting\" first.";
        var layout = AnswerCitationLayout.Build(text, [Citation("VPN Connection Troubleshooting")]);

        var badge = layout.Runs.OfType<AnswerBadgeRun>().Should().ContainSingle().Subject;
        badge.Citation.Number.Should().Be(1);
        layout.UnanchoredCitations.Should().BeEmpty();

        // Badge sits right after the closing quote, before the remaining text.
        var runs = layout.Runs.ToList();
        var badgeIndex = runs.IndexOf(badge);
        runs[badgeIndex - 1].Should().BeOfType<AnswerTextRun>()
            .Which.Text.Should().EndWith("\"");
        runs[badgeIndex + 1].Should().BeOfType<AnswerTextRun>()
            .Which.Text.Should().Be(" first.");
    }

    [Fact]
    public void TitleAbsent_FallsBackToUnanchored()
    {
        var layout = AnswerCitationLayout.Build("A generic answer.", [Citation("VPN Connection Troubleshooting")]);

        layout.Runs.Should().ContainSingle().Which.Should().BeOfType<AnswerTextRun>();
        layout.UnanchoredCitations.Should().ContainSingle()
            .Which.Number.Should().Be(1);
    }

    [Fact]
    public void MixedCitations_AnchorsOneAndKeepsOtherAsFallback()
    {
        var text = "See \"Password Reset\" for the lockout policy.";
        var citations = new[]
        {
            Citation("Password Reset"),
            Citation("VPN Connection Troubleshooting"),
        };

        var layout = AnswerCitationLayout.Build(text, citations);

        layout.Runs.OfType<AnswerBadgeRun>().Should().ContainSingle()
            .Which.Citation.Number.Should().Be(1);
        layout.UnanchoredCitations.Should().ContainSingle()
            .Which.Citation.Title.Should().Be("VPN Connection Troubleshooting");
    }

    [Fact]
    public void TitleMatch_IsCaseInsensitive()
    {
        var layout = AnswerCitationLayout.Build("about password reset today", [Citation("Password Reset")]);

        layout.Runs.OfType<AnswerBadgeRun>().Should().ContainSingle();
        layout.UnanchoredCitations.Should().BeEmpty();
    }

    [Fact]
    public void SameTitleTwice_EmitsBothBadgesAtTheMention()
    {
        var text = "Per \"VPN Connection Troubleshooting\".";
        var citations = new[]
        {
            Citation("VPN Connection Troubleshooting", "Symptoms"),
            Citation("VPN Connection Troubleshooting", "First Checks"),
        };

        var layout = AnswerCitationLayout.Build(text, citations);

        layout.Runs.OfType<AnswerBadgeRun>().Select(b => b.Citation.Number)
            .Should().Equal(1, 2);
        layout.UnanchoredCitations.Should().BeEmpty();
    }

    [Fact]
    public void EmptyText_KeepsCitationsAsFallback()
    {
        var layout = AnswerCitationLayout.Build(string.Empty, [Citation("Password Reset")]);

        layout.Runs.OfType<AnswerBadgeRun>().Should().BeEmpty();
        layout.UnanchoredCitations.Should().ContainSingle();
    }
}
