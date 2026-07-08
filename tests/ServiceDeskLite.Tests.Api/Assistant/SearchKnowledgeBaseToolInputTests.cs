using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Application.Abstractions.Search;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class SearchKnowledgeBaseToolInputTests
{
    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void TryParseInput_WithQueryOnly_UsesDefaultLimit()
    {
        var input = Json("""{ "query": "how do I reset a locked account?" }""");

        var ok = SearchKnowledgeBaseTool.TryParseInput(input, out var query, out var limit, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        query.Should().Be("how do I reset a locked account?");
        limit.Should().Be(4);
    }

    [Fact]
    public void TryParseInput_WithExplicitLimit_UsesIt()
    {
        var input = Json("""{ "query": "vpn", "limit": 2 }""");

        var ok = SearchKnowledgeBaseTool.TryParseInput(input, out _, out var limit, out _);

        ok.Should().BeTrue();
        limit.Should().Be(2);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "query": "" }""")]
    [InlineData("""{ "query": "   " }""")]
    [InlineData("""{ "query": 42 }""")]
    public void TryParseInput_WithMissingOrInvalidQuery_Fails(string json)
    {
        var ok = SearchKnowledgeBaseTool.TryParseInput(Json(json), out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("query");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-1)]
    public void TryParseInput_WithLimitOutOfRange_Fails(int limit)
    {
        var input = Json($$"""{ "query": "q", "limit": {{limit}} }""");

        var ok = SearchKnowledgeBaseTool.TryParseInput(input, out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("limit");
    }

    [Fact]
    public void TryParseInput_WithNonObjectInput_Fails()
    {
        var ok = SearchKnowledgeBaseTool.TryParseInput(Json("\"just a string\""), out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("JSON object");
    }

    [Fact]
    public void FormatResult_ListsSourcesWithHeadingAndSimilarity()
    {
        var matches = new[]
        {
            new KnowledgeMatch("vpn", "VPN Connection Troubleshooting", "FAQ", "Authentication Failures",
                "Clear the saved credential and sign in again.", 0.83),
        };

        var text = SearchKnowledgeBaseTool.FormatResult("vpn auth fails", matches);

        text.Should().Contain("Found 1 knowledge-base passage(s)");
        text.Should().Contain("\"VPN Connection Troubleshooting\"");
        text.Should().Contain("(FAQ)");
        text.Should().Contain("Authentication Failures");
        text.Should().Contain("similarity=83");
        text.Should().Contain("Clear the saved credential");
    }

    [Fact]
    public void ToCitations_MapsFieldsAndTruncatesLongSnippets()
    {
        var longBody = new string('x', 400);
        var matches = new[]
        {
            new KnowledgeMatch("a", "Title", "Article", "Heading", longBody, 0.9),
        };

        var citations = SearchKnowledgeBaseTool.ToCitations(matches);

        citations.Should().HaveCount(1);
        citations[0].Title.Should().Be("Title");
        citations[0].Source.Should().Be("Article");
        citations[0].Heading.Should().Be("Heading");
        citations[0].Similarity.Should().Be(0.9);
        citations[0].Snippet.Length.Should().BeLessThan(longBody.Length);
        citations[0].Snippet.Should().EndWith("…");
    }
}
