using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class FindSimilarTicketsToolInputTests
{
    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void TryParseInput_WithQueryOnly_UsesDefaultsAndNoFilters()
    {
        var input = Json("""{ "query": "printer on floor 3 not printing" }""");

        var ok = FindSimilarTicketsTool.TryParseInput(input, out var query, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        query.Text.Should().Be("printer on floor 3 not printing");
        query.Limit.Should().Be(5);
        query.Statuses.Should().BeNull();
        query.Priorities.Should().BeNull();
    }

    [Fact]
    public void TryParseInput_WithExplicitLimit_UsesIt()
    {
        var ok = FindSimilarTicketsTool.TryParseInput(Json("""{ "query": "vpn broken", "limit": 3 }"""), out var query, out _);

        ok.Should().BeTrue();
        query.Limit.Should().Be(3);
    }

    [Fact]
    public void TryParseInput_WithMetadataFilters_ParsesEnums()
    {
        var input = Json("""{ "query": "vpn", "statuses": ["New", "InProgress"], "priorities": ["High", "Critical"] }""");

        var ok = FindSimilarTicketsTool.TryParseInput(input, out var query, out _);

        ok.Should().BeTrue();
        query.Statuses.Should().BeEquivalentTo([TicketStatus.New, TicketStatus.InProgress]);
        query.Priorities.Should().BeEquivalentTo([TicketPriority.High, TicketPriority.Critical]);
    }

    [Fact]
    public void TryParseInput_WithCaseInsensitiveEnum_Parses()
    {
        var ok = FindSimilarTicketsTool.TryParseInput(Json("""{ "query": "q", "priorities": ["critical"] }"""), out var query, out _);

        ok.Should().BeTrue();
        query.Priorities.Should().BeEquivalentTo([TicketPriority.Critical]);
    }

    [Fact]
    public void TryParseInput_WithEmptyFilterArray_TreatsAsNoFilter()
    {
        var ok = FindSimilarTicketsTool.TryParseInput(Json("""{ "query": "q", "statuses": [] }"""), out var query, out _);

        ok.Should().BeTrue();
        query.Statuses.Should().BeNull();
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "query": "" }""")]
    [InlineData("""{ "query": "   " }""")]
    [InlineData("""{ "query": 42 }""")]
    public void TryParseInput_WithMissingOrInvalidQuery_Fails(string json)
    {
        var ok = FindSimilarTicketsTool.TryParseInput(Json(json), out _, out var error);

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

        var ok = FindSimilarTicketsTool.TryParseInput(input, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("limit");
    }

    [Fact]
    public void TryParseInput_WithInvalidStatus_Fails()
    {
        var ok = FindSimilarTicketsTool.TryParseInput(Json("""{ "query": "q", "statuses": ["Nope"] }"""), out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("statuses");
    }

    [Fact]
    public void TryParseInput_WithNonArrayFilter_Fails()
    {
        var ok = FindSimilarTicketsTool.TryParseInput(Json("""{ "query": "q", "priorities": "High" }"""), out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("priorities");
    }

    [Fact]
    public void TryParseInput_WithNonObjectInput_Fails()
    {
        var ok = FindSimilarTicketsTool.TryParseInput(Json("\"just a string\""), out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("JSON object");
    }
}
