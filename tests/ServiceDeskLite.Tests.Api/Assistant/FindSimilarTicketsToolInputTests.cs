using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class FindSimilarTicketsToolInputTests
{
    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void TryParseInput_WithQueryOnly_UsesDefaultLimit()
    {
        var input = Json("""{ "query": "printer on floor 3 not printing" }""");

        var ok = FindSimilarTicketsTool.TryParseInput(input, out var query, out var limit, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        query.Should().Be("printer on floor 3 not printing");
        limit.Should().Be(5);
    }

    [Fact]
    public void TryParseInput_WithExplicitLimit_UsesIt()
    {
        var input = Json("""{ "query": "vpn broken", "limit": 3 }""");

        var ok = FindSimilarTicketsTool.TryParseInput(input, out _, out var limit, out _);

        ok.Should().BeTrue();
        limit.Should().Be(3);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "query": "" }""")]
    [InlineData("""{ "query": "   " }""")]
    [InlineData("""{ "query": 42 }""")]
    public void TryParseInput_WithMissingOrInvalidQuery_Fails(string json)
    {
        var ok = FindSimilarTicketsTool.TryParseInput(Json(json), out _, out _, out var error);

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

        var ok = FindSimilarTicketsTool.TryParseInput(input, out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("limit");
    }

    [Fact]
    public void TryParseInput_WithNonObjectInput_Fails()
    {
        var ok = FindSimilarTicketsTool.TryParseInput(Json("\"just a string\""), out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("JSON object");
    }

    [Fact]
    public void FormatResult_WithMatches_ListsOnePerLineWithPercentSimilarity()
    {
        var id = new TicketId(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        var matches = new[]
        {
            new TicketSimilarityMatch(id, "Printer offline", TicketStatus.New, TicketPriority.High, 0.87),
        };

        var text = FindSimilarTicketsTool.FormatResult("printer broken", matches);

        text.Should().Contain("Found 1 ticket(s)");
        text.Should().Contain("11111111-2222-3333-4444-555555555555");
        text.Should().Contain("\"Printer offline\"");
        text.Should().Contain("status=New");
        text.Should().Contain("priority=High");
        text.Should().Contain("similarity=87");
    }

    [Fact]
    public void FormatResult_WithoutMatches_SaysSo()
    {
        var text = FindSimilarTicketsTool.FormatResult("anything", []);

        text.Should().Contain("No tickets similar");
    }
}
