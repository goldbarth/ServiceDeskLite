using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Application.Abstractions.Routing;
using ServiceDeskLite.Application.Tickets.Routing;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class RouteTicketToolTests
{
    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement;

    [Fact]
    public void TryParseInput_ValidTicketId_Succeeds()
    {
        var id = Guid.NewGuid();

        var ok = RouteTicketTool.TryParseInput(Json($$"""{"ticketId":"{{id}}"}"""), out var parsed, out var error);

        ok.Should().BeTrue();
        parsed.Should().Be(id);
        error.Should().BeNull();
    }

    [Fact]
    public void TryParseInput_MissingTicketId_Fails()
    {
        var ok = RouteTicketTool.TryParseInput(Json("""{}"""), out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("ticketId");
    }

    [Fact]
    public void TryParseInput_NonObject_Fails()
    {
        var ok = RouteTicketTool.TryParseInput(Json("""[]"""), out _, out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void FormatResult_Applied_ReportsChangesAndConfidence()
    {
        var id = Guid.NewGuid();
        var decision = new RoutingDecision(
            TicketCategory.Network, TicketPriority.Critical, "Alex Kim", TicketStatus.Triaged, 0.9, "rationale text");
        var result = new RouteTicketResult(true, decision, ["priority=Critical, category=Network", "assigned to Alex Kim"]);

        var text = RouteTicketTool.FormatResult(id, result);

        text.Should().Contain("Routed ticket");
        text.Should().Contain("90%");
        text.Should().Contain("assigned to Alex Kim");
        text.Should().NotContain("NOT applied");
    }

    [Fact]
    public void FormatResult_Suggestion_MarksNotAppliedAndListsSuggestion()
    {
        var id = Guid.NewGuid();
        var decision = new RoutingDecision(
            TicketCategory.Other, TicketPriority.Medium, null, TicketStatus.Triaged, 0.3, "no keyword matched");
        var result = new RouteTicketResult(false, decision, []);

        var text = RouteTicketTool.FormatResult(id, result);

        text.Should().Contain("NOT applied");
        text.Should().Contain("30%");
        text.Should().Contain("assignee=none");
    }
}
