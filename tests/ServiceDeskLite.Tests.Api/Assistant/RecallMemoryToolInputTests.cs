using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class RecallMemoryToolInputTests
{
    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void TryParseInput_WithQueryOnly_UsesDefaultLimit()
    {
        var input = Json("""{ "query": "preferred priority for vpn" }""");

        var ok = RecallMemoryTool.TryParseInput(input, out var query, out var limit, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        query.Should().Be("preferred priority for vpn");
        limit.Should().Be(5);
    }

    [Fact]
    public void TryParseInput_WithExplicitLimit_UsesIt()
    {
        var input = Json("""{ "query": "contact method", "limit": 2 }""");

        var ok = RecallMemoryTool.TryParseInput(input, out _, out var limit, out _);

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
        var ok = RecallMemoryTool.TryParseInput(Json(json), out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("query");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-1)]
    public void TryParseInput_WithLimitOutOfRange_Fails(int limit)
    {
        var input = Json($$"""{ "query": "q", "limit": {{limit}} }""");

        var ok = RecallMemoryTool.TryParseInput(input, out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("limit");
    }

    [Fact]
    public void FormatResult_WithMatches_ListsOnePerLineWithKindAndRelevance()
    {
        var matches = new[]
        {
            new MemoryMatch(MemoryId.New(), "Prefers high priority for VPN issues", "preference", 0.91),
        };

        var text = RecallMemoryTool.FormatResult("vpn priority", matches);

        text.Should().Contain("Found 1");
        text.Should().Contain("[preference]");
        text.Should().Contain("Prefers high priority for VPN issues");
        text.Should().Contain("relevance=91");
    }

    [Fact]
    public void FormatResult_WithoutMatches_SaysSo()
    {
        var text = RecallMemoryTool.FormatResult("anything", []);

        text.Should().Contain("No stored memories");
    }
}
