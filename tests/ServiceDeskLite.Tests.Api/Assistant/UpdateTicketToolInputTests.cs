using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class UpdateTicketToolInputTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TicketGuid = Guid.Parse("019f2263-1d67-775a-a5f1-f916975e4258");

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void TryParseInput_PartialUpdate_MapsCommand()
    {
        var input = Json($$"""
            {
              "ticketId": "{{TicketGuid}}",
              "priority": "Critical",
              "dueAt": "2026-07-03T09:00:00+02:00"
            }
            """);

        var ok = UpdateTicketTool.TryParseInput(input, Now, out var command, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        command!.Id.Should().Be(new TicketId(TicketGuid));
        command.Title.Should().BeNull();
        command.Description.Should().BeNull();
        command.Priority.Should().Be(TicketPriority.Critical);
        command.DueAt.Should().Be(new DateTimeOffset(2026, 7, 3, 9, 0, 0, TimeSpan.FromHours(2)));
        command.Actor.Should().Be("ai-assistant");
    }

    [Fact]
    public void TryParseInput_MissingTicketId_Fails()
    {
        var input = Json("""{ "priority": "High" }""");

        var ok = UpdateTicketTool.TryParseInput(input, Now, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("ticketId");
    }

    [Fact]
    public void TryParseInput_MissingTicketId_ErrorNamesResolvableSources()
    {
        var input = Json("""{ "priority": "High" }""");

        UpdateTicketTool.TryParseInput(input, Now, out _, out var error);

        error.Should().Contain("create_ticket").And.Contain("search_tickets");
    }

    [Fact]
    public void TryParseInput_InvalidTicketId_Fails()
    {
        var input = Json("""{ "ticketId": "not-a-guid", "priority": "High" }""");

        var ok = UpdateTicketTool.TryParseInput(input, Now, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("ticketId");
    }

    [Fact]
    public void TryParseInput_NoUpdatableFields_Fails()
    {
        var input = Json($$"""{ "ticketId": "{{TicketGuid}}" }""");

        var ok = UpdateTicketTool.TryParseInput(input, Now, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("At least one");
    }

    [Fact]
    public void TryParseInput_DueAtInPast_Fails_AndErrorNamesCurrentDate()
    {
        var input = Json($$"""
            { "ticketId": "{{TicketGuid}}", "dueAt": "2026-06-27T09:00:00+02:00" }
            """);

        var ok = UpdateTicketTool.TryParseInput(input, Now, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("in the past").And.Contain("2026-07-02");
    }

    [Fact]
    public void TryParseInput_UnknownPriority_Fails()
    {
        var input = Json($$"""{ "ticketId": "{{TicketGuid}}", "priority": "Urgent" }""");

        var ok = UpdateTicketTool.TryParseInput(input, Now, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("priority");
    }
}
