using FluentAssertions;

using ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;

namespace ServiceDeskLite.Tests.Api.KnowledgeBase;

public sealed class FileKnowledgeCorpusParseTests
{
    [Fact]
    public void Parse_ReadsFrontMatterTitleAndSource()
    {
        var text =
            """
            ---
            title: VPN Connection Troubleshooting
            source: FAQ
            ---

            ## Symptoms
            body.
            """;

        var article = FileKnowledgeCorpus.Parse("vpn-connection-troubleshooting", text);

        article.Id.Should().Be("vpn-connection-troubleshooting");
        article.Title.Should().Be("VPN Connection Troubleshooting");
        article.Source.Should().Be("FAQ");
        article.Body.Should().StartWith("## Symptoms");
        article.Body.Should().NotContain("title:");
    }

    [Fact]
    public void Parse_WithoutFrontMatter_FallsBackToSlugAndDefaultSource()
    {
        var article = FileKnowledgeCorpus.Parse("some-doc", "## Heading\nbody.");

        article.Title.Should().Be("some-doc");
        article.Source.Should().Be("Article");
        article.Body.Should().Contain("## Heading");
    }
}
