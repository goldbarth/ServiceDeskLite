using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentAssertions;

using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Tests.Evaluation.Harness;

namespace ServiceDeskLite.Tests.Evaluation.Scenarios;

/// <summary>
/// The thinking policy of ADR-0038, asserted on the wire rather than on a property.
/// </summary>
/// <remarks>
/// Sonnet 5 turns adaptive thinking on when the request omits the field; Opus 4.8 does not. A call
/// site that forgets to set it is therefore not a compile error and not an API error - it silently
/// spends the MaxTokens budget on reasoning nobody asked for and delays the first SSE delta. The
/// only place that shows is the request body the SDK actually sent.
/// </remarks>
public sealed class ThinkingPolicyTests
{
    [Fact]
    public async Task Every_model_request_from_the_agent_loop_disables_thinking()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create().Calls("search_tickets", new { searchTerm = "printer" }))
            .Then(ScriptedTurn.Create().Says("Nothing found."));

        var client = host.CreateClient();
        await client.ChatAsync("Any printer tickets?");

        // Both round trips, not just the first: the tool result turn is a fresh request.
        host.Model.RequestCount.Should().Be(2);
        foreach (var request in host.Model.Requests)
            ThinkingTypeOf(request).Should().Be("disabled");
    }

    [Fact]
    public async Task The_summary_request_disables_thinking_too()
    {
        using var host = new EvaluationHost();
        host.Model
            .Then(ScriptedTurn.Create()
                .Calls("create_ticket", new { title = "Printer offline", description = "Third floor printer is dead.", priority = "High" }))
            .Then(ScriptedTurn.Create().Says("Filed."))
            .Then(ScriptedTurn.Create().Says("A short summary."));

        var client = host.CreateClient();
        await client.ChatAsync("The third floor printer is dead.");

        var json = new JsonSerializerOptions(JsonSerializerOptions.Web) { Converters = { new JsonStringEnumConverter() } };
        var tickets = await client.GetFromJsonAsync<PagedResponse<TicketListItemResponse>>(
            "/api/v1/tickets?searchTerm=Printer offline", json);
        var ticketId = tickets!.Items.Single().Id;

        await client.GetStringAsync($"/api/v1/tickets/{ticketId}/summary");

        // TicketSummaryService builds its own request, on the tightest MaxTokens in the system.
        host.Model.RequestCount.Should().Be(3);
        ThinkingTypeOf(host.Model.Requests[^1]).Should().Be("disabled");
    }

    private static string? ThinkingTypeOf(JsonElement request) =>
        request.TryGetProperty("thinking", out var thinking)
            ? thinking.GetProperty("type").GetString()
            : null;
}
