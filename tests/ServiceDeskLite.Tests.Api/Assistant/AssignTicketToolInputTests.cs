using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class AssignTicketToolInputTests
{
    private static readonly Guid TicketGuid = Guid.Parse("019f2263-1d67-775a-a5f1-f916975e4258");

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void TryParseInput_WithAssignee_MapsTicketIdAndName()
    {
        var input = Json($$"""{ "ticketId": "{{TicketGuid}}", "assignee": "Alex Kim" }""");

        var ok = AssignTicketTool.TryParseInput(input, out var ticketId, out var assigneeName, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        ticketId.Should().Be(TicketGuid);
        assigneeName.Should().Be("Alex Kim");
    }

    [Fact]
    public void TryParseInput_WithoutAssignee_MeansUnassign()
    {
        var input = Json($$"""{ "ticketId": "{{TicketGuid}}" }""");

        var ok = AssignTicketTool.TryParseInput(input, out var ticketId, out var assigneeName, out _);

        ok.Should().BeTrue();
        ticketId.Should().Be(TicketGuid);
        assigneeName.Should().BeNull();
    }

    [Fact]
    public void TryParseInput_BlankAssignee_NormalisesToNull()
    {
        var input = Json($$"""{ "ticketId": "{{TicketGuid}}", "assignee": "   " }""");

        var ok = AssignTicketTool.TryParseInput(input, out _, out var assigneeName, out _);

        ok.Should().BeTrue();
        assigneeName.Should().BeNull();
    }

    [Fact]
    public void TryParseInput_TrimsAssignee()
    {
        var input = Json($$"""{ "ticketId": "{{TicketGuid}}", "assignee": "  Alex Kim  " }""");

        AssignTicketTool.TryParseInput(input, out _, out var assigneeName, out _);

        assigneeName.Should().Be("Alex Kim");
    }

    [Fact]
    public void TryParseInput_MissingTicketId_Fails_AndNamesResolvableSources()
    {
        var input = Json("""{ "assignee": "Alex Kim" }""");

        var ok = AssignTicketTool.TryParseInput(input, out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("ticketId").And.Contain("search_tickets");
    }

    [Fact]
    public void TryParseInput_InvalidTicketId_Fails()
    {
        var input = Json("""{ "ticketId": "not-a-guid" }""");

        var ok = AssignTicketTool.TryParseInput(input, out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("ticketId");
    }

    [Fact]
    public void TryParseInput_NonObjectInput_Fails()
    {
        var ok = AssignTicketTool.TryParseInput(Json("\"nope\""), out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("JSON object");
    }
}
