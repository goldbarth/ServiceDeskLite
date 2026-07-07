using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class ChangeTicketStatusToolInputTests
{
    private static readonly Guid TicketGuid = Guid.Parse("019f2263-1d67-775a-a5f1-f916975e4258");

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void TryParseInput_ValidInput_MapsCommandWithAssistantActor()
    {
        var input = Json($$"""{ "ticketId": "{{TicketGuid}}", "status": "InProgress" }""");

        var ok = ChangeTicketStatusTool.TryParseInput(input, out var command, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        command!.Id.Should().Be(new TicketId(TicketGuid));
        command.NewStatus.Should().Be(TicketStatus.InProgress);
        command.Actor.Should().Be("ai-assistant");
    }

    [Fact]
    public void TryParseInput_StatusIsCaseInsensitive()
    {
        var input = Json($$"""{ "ticketId": "{{TicketGuid}}", "status": "closed" }""");

        var ok = ChangeTicketStatusTool.TryParseInput(input, out var command, out _);

        ok.Should().BeTrue();
        command!.NewStatus.Should().Be(TicketStatus.Closed);
    }

    [Fact]
    public void TryParseInput_MissingTicketId_Fails_AndNamesResolvableSources()
    {
        var input = Json("""{ "status": "Triaged" }""");

        var ok = ChangeTicketStatusTool.TryParseInput(input, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("ticketId").And.Contain("search_tickets");
    }

    [Fact]
    public void TryParseInput_InvalidTicketId_Fails()
    {
        var input = Json("""{ "ticketId": "not-a-guid", "status": "Triaged" }""");

        var ok = ChangeTicketStatusTool.TryParseInput(input, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("ticketId");
    }

    [Fact]
    public void TryParseInput_MissingStatus_Fails()
    {
        var input = Json($$"""{ "ticketId": "{{TicketGuid}}" }""");

        var ok = ChangeTicketStatusTool.TryParseInput(input, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("status");
    }

    [Fact]
    public void TryParseInput_UnknownStatus_Fails()
    {
        var input = Json($$"""{ "ticketId": "{{TicketGuid}}", "status": "Archived" }""");

        var ok = ChangeTicketStatusTool.TryParseInput(input, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("status");
    }

    [Fact]
    public void TryParseInput_NonObjectInput_Fails()
    {
        var ok = ChangeTicketStatusTool.TryParseInput(Json("\"just a string\""), out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("JSON object");
    }
}
