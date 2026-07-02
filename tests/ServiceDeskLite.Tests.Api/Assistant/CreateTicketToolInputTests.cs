using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class CreateTicketToolInputTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void TryParseInput_WithAllFields_MapsCommand()
    {
        var input = Json("""
            {
              "title": "Printer offline",
              "description": "The office printer on floor 3 does not respond.",
              "priority": "High",
              "dueAt": "2026-07-04T10:00:00+00:00"
            }
            """);

        var ok = CreateTicketTool.TryParseInput(input, CreatedAt, out var command, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        command!.Title.Should().Be("Printer offline");
        command.Description.Should().Be("The office printer on floor 3 does not respond.");
        command.Priority.Should().Be(TicketPriority.High);
        command.CreatedAt.Should().Be(CreatedAt);
        command.DueAt.Should().Be(new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero));
        command.Actor.Should().Be("ai-assistant");
    }

    [Fact]
    public void TryParseInput_WithoutDueAt_LeavesDueAtNull()
    {
        var input = Json("""
            { "title": "VPN broken", "description": "Cannot connect since this morning.", "priority": "Medium" }
            """);

        var ok = CreateTicketTool.TryParseInput(input, CreatedAt, out var command, out _);

        ok.Should().BeTrue();
        command!.DueAt.Should().BeNull();
    }

    [Fact]
    public void TryParseInput_PriorityIsCaseInsensitive()
    {
        var input = Json("""
            { "title": "t", "description": "d", "priority": "critical" }
            """);

        var ok = CreateTicketTool.TryParseInput(input, CreatedAt, out var command, out _);

        ok.Should().BeTrue();
        command!.Priority.Should().Be(TicketPriority.Critical);
    }

    [Theory]
    [InlineData("""{ "description": "d", "priority": "Low" }""", "title")]
    [InlineData("""{ "title": "t", "priority": "Low" }""", "description")]
    [InlineData("""{ "title": "t", "description": "d" }""", "priority")]
    public void TryParseInput_MissingRequiredField_Fails(string json, string expectedField)
    {
        var ok = CreateTicketTool.TryParseInput(Json(json), CreatedAt, out var command, out var error);

        ok.Should().BeFalse();
        command.Should().BeNull();
        error.Should().Contain(expectedField);
    }

    [Fact]
    public void TryParseInput_UnknownPriority_Fails()
    {
        var input = Json("""
            { "title": "t", "description": "d", "priority": "Urgent" }
            """);

        var ok = CreateTicketTool.TryParseInput(input, CreatedAt, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("priority");
    }

    [Fact]
    public void TryParseInput_DueAtInPast_Fails_AndErrorNamesCurrentDate()
    {
        var input = Json("""
            { "title": "t", "description": "d", "priority": "Low", "dueAt": "2026-06-27T00:00:00+00:00" }
            """);

        var ok = CreateTicketTool.TryParseInput(input, CreatedAt, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("in the past").And.Contain("2026-07-02");
    }

    [Fact]
    public void TryParseInput_InvalidDueAt_Fails()
    {
        var input = Json("""
            { "title": "t", "description": "d", "priority": "Low", "dueAt": "tomorrow" }
            """);

        var ok = CreateTicketTool.TryParseInput(input, CreatedAt, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("dueAt");
    }

    [Fact]
    public void TryParseInput_NonObjectInput_Fails()
    {
        var ok = CreateTicketTool.TryParseInput(Json("\"just a string\""), CreatedAt, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("JSON object");
    }
}
