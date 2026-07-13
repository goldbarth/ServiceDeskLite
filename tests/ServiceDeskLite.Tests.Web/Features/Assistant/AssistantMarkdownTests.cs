using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Assistant;
using ServiceDeskLite.Web.Features.Assistant;

namespace ServiceDeskLite.Tests.Web.Features.Assistant;

/// <summary>
/// Assistant output embeds whatever a ticket filer wrote, echoed back by the model.
/// These tests pin the two properties the page relies on before it hands the result to
/// <c>MarkupString</c>: markup renders, hostile content does not.
/// </summary>
public sealed class AssistantMarkdownTests
{
    [Fact]
    public void RendersEmphasisAndLists()
    {
        var html = AssistantMarkdown.ToHtml("**bold** and:\n- one\n- two");

        html.Should().Contain("<strong>bold</strong>");
        html.Should().Contain("<ul>").And.Contain("<li>one</li>");
    }

    [Fact]
    public void RendersHeadingsAndCode()
    {
        var html = AssistantMarkdown.ToHtml("## Steps\n`dotnet build`");

        html.Should().Contain("<h2>Steps</h2>");
        html.Should().Contain("<code>dotnet build</code>");
    }

    [Fact]
    public void EscapesRawHtmlFromHostileTicketTitle()
    {
        var html = AssistantMarkdown.ToHtml(
            "The ticket \"<img src=x onerror=alert(1)> printer down\" was created.");

        html.Should().NotContain("<img");
        html.Should().Contain("&lt;img");
    }

    [Fact]
    public void EscapesScriptTags()
    {
        var html = AssistantMarkdown.ToHtml("<script>alert(1)</script>");

        html.Should().NotContain("<script");
        html.Should().Contain("&lt;script");
    }

    [Theory]
    [InlineData("[click](javascript:alert(1))")]
    [InlineData("[click](JaVaScRiPt:alert(1))")]
    [InlineData("[click](vbscript:x)")]
    [InlineData("![img](data:text/html;base64,PHNjcmlwdD4=)")]
    public void NeutralizesUnsafeLinkSchemes(string markdown)
    {
        var html = AssistantMarkdown.ToHtml(markdown);

        html.Should().NotContainEquivalentOf("href=\"javascript");
        html.Should().NotContainEquivalentOf("href=\"vbscript");
        html.Should().NotContainEquivalentOf("src=\"data:");
    }

    [Fact]
    public void NeutralizesUnsafeAutolinks()
    {
        var html = AssistantMarkdown.ToHtml("<javascript:alert(1)>");

        html.Should().NotContainEquivalentOf("href=\"javascript");
    }

    [Theory]
    [InlineData("[docs](https://example.com/kb)", "href=\"https://example.com/kb\"")]
    [InlineData("[ticket](/tickets/42)", "href=\"/tickets/42\"")]
    [InlineData("[mail](mailto:it@example.com)", "href=\"mailto:it@example.com\"")]
    public void KeepsSafeLinks(string markdown, string expectedHref)
    {
        AssistantMarkdown.ToHtml(markdown).Should().Contain(expectedHref);
    }

    [Fact]
    public void KeepsRelativeLinkWithColonInQuery()
    {
        var html = AssistantMarkdown.ToHtml("[search](/tickets?q=a:b)");

        html.Should().Contain("href=\"/tickets?q=a:b\"");
    }

    [Fact]
    public void ReturnsEmptyForWhitespace()
    {
        AssistantMarkdown.ToHtml("  \n ").Should().BeEmpty();
    }

    [Fact]
    public void ToleratesTruncatedMarkdown()
    {
        // A message cut mid-fence must still convert; the page only converts completed
        // messages, but a stream can end early on an error.
        var html = AssistantMarkdown.ToHtml("Steps:\n```bash\ndotnet bu");

        html.Should().Contain("dotnet bu");
    }

    private static AssistantCitation Citation(string title, string source = "kb.md", string heading = "Section", string snippet = "snippet") =>
        new(title, source, heading, snippet, 0.9);

    [Fact]
    public void RenderWithoutCitationsMatchesToHtml()
    {
        var render = AssistantMarkdown.Render("**bold**", []);

        render.Html.Should().Be(AssistantMarkdown.ToHtml("**bold**"));
        render.Unanchored.Should().BeEmpty();
    }

    [Fact]
    public void AnchorsBadgeAfterCitedTitle()
    {
        var render = AssistantMarkdown.Render(
            "See the Printer Setup guide for the steps.",
            [Citation("Printer Setup")]);

        render.Html.Should().Contain("Printer Setup");
        render.Html.Should().Contain("assistant-chat__cite");
        // Numbered by position, so the badge and the sources list agree.
        render.Html.Should().Contain("[1]");
        render.Unanchored.Should().BeEmpty();
    }

    [Fact]
    public void NumbersBadgesByCitationPosition()
    {
        var render = AssistantMarkdown.Render(
            "First check Alpha, then Beta.",
            [Citation("Alpha"), Citation("Beta")]);

        var alphaBadge = render.Html.IndexOf("[1]", StringComparison.Ordinal);
        var betaBadge = render.Html.IndexOf("[2]", StringComparison.Ordinal);

        alphaBadge.Should().BeGreaterThan(0);
        betaBadge.Should().BeGreaterThan(alphaBadge);
    }

    [Fact]
    public void ReportsCitationWhoseTitleIsAbsentAsUnanchored()
    {
        var render = AssistantMarkdown.Render(
            "Nothing relevant here.",
            [Citation("Printer Setup")]);

        render.Html.Should().NotContain("assistant-chat__cite\"");
        render.Unanchored.Should().ContainSingle().Which.Title.Should().Be("Printer Setup");
    }

    [Fact]
    public void EncodesHostileCitationContentInBadge()
    {
        var render = AssistantMarkdown.Render(
            "See the guide named guide.",
            [Citation("guide", snippet: "<img src=x onerror=alert(1)>")]);

        render.Html.Should().NotContain("<img");
        render.Html.Should().Contain("&lt;img");
    }

    [Fact]
    public void ModelCannotForgeABadgeWithSentinelCharacters()
    {
        // The model echoes ticket content; a smuggled sentinel must not reach the badge swap.
        var render = AssistantMarkdown.Render(
            "Fake 0 badge, real guide here.",
            [Citation("guide")]);

        // Exactly one real badge, from the anchored "guide" title.
        System.Text.RegularExpressions.Regex
            .Matches(render.Html, "assistant-chat__cite\"")
            .Should().HaveCount(1);
    }

    [Fact]
    public void OrdersCoLocatedBadgesAscending()
    {
        // Two passages from the same article share one title, so both badges anchor at the same
        // spot; they must read [1][2], not the reverse of the insertion order.
        var render = AssistantMarkdown.Render(
            "See the VPN Guide for the fix.",
            [Citation("VPN Guide", heading: "Auth"), Citation("VPN Guide", heading: "Symptoms")]);

        var one = render.Html.IndexOf("[1]", StringComparison.Ordinal);
        var two = render.Html.IndexOf("[2]", StringComparison.Ordinal);

        one.Should().BeGreaterThan(0);
        two.Should().BeGreaterThan(one);
    }

    [Fact]
    public void PlacesBadgeAfterClosingQuote()
    {
        var render = AssistantMarkdown.Render(
            "The “Printer Setup” article explains it.",
            [Citation("Printer Setup")]);

        var quoteThenBadge = render.Html.IndexOf("”", StringComparison.Ordinal);
        var badge = render.Html.IndexOf("[1]", StringComparison.Ordinal);

        badge.Should().BeGreaterThan(quoteThenBadge);
    }
}
