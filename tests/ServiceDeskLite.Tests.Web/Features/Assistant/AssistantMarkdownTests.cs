using FluentAssertions;

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
}
