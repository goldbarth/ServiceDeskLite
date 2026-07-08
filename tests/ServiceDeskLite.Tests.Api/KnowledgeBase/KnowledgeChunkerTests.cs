using FluentAssertions;

using ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;

namespace ServiceDeskLite.Tests.Api.KnowledgeBase;

public sealed class KnowledgeChunkerTests
{
    [Fact]
    public void Chunk_SplitsOnLevelTwoHeadings()
    {
        var article = new KnowledgeArticle("vpn", "VPN Guide", "FAQ",
            """
            ## First
            first body.

            ## Second
            second body.
            """);

        var sections = KnowledgeChunker.Chunk(article);

        sections.Should().HaveCount(2);
        sections[0].Heading.Should().Be("First");
        sections[0].Ordinal.Should().Be(0);
        sections[0].Content.Should().Contain("first body");
        sections[1].Heading.Should().Be("Second");
        sections[1].Ordinal.Should().Be(1);
    }

    [Fact]
    public void Chunk_ContentBeforeFirstHeading_BecomesOverview()
    {
        var article = new KnowledgeArticle("a", "A", "Article",
            """
            intro paragraph.

            ## Details
            detail body.
            """);

        var sections = KnowledgeChunker.Chunk(article);

        sections.Should().HaveCount(2);
        sections[0].Heading.Should().Be("Overview");
        sections[0].Content.Should().Contain("intro paragraph");
    }

    [Fact]
    public void Chunk_SkipsEmptySections()
    {
        var article = new KnowledgeArticle("a", "A", "Article",
            """
            ## Empty

            ## Real
            content.
            """);

        var sections = KnowledgeChunker.Chunk(article);

        sections.Should().ContainSingle();
        sections[0].Heading.Should().Be("Real");
    }

    [Fact]
    public void SectionId_IsDeterministicPerArticleAndOrdinal()
    {
        KnowledgeChunker.SectionId("vpn", 0).Should().Be(KnowledgeChunker.SectionId("vpn", 0));
        KnowledgeChunker.SectionId("vpn", 0).Should().NotBe(KnowledgeChunker.SectionId("vpn", 1));
        KnowledgeChunker.SectionId("vpn", 0).Should().NotBe(KnowledgeChunker.SectionId("dns", 0));
    }
}
