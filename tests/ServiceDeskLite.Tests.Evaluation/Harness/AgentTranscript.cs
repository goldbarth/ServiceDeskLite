using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;

namespace ServiceDeskLite.Tests.Evaluation.Harness;

/// <summary>What the browser would see: the ordered SSE events the agent emitted.</summary>
public sealed record AgentEvent(string Type, JsonElement Data)
{
    public string? Text => Read("text");
    public string? ToolName => Read("toolName");
    public string? Message => Read("message");
    public bool IsError => Data.TryGetProperty("isError", out var e) && e.GetBoolean();

    private string? Read(string property)
        => Data.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public static class AgentTranscript
{
    public const string ConversationEvent = "conversation";
    public const string TextEvent = "text";
    public const string ToolCallEvent = "tool_call";
    public const string ToolResultEvent = "tool_result";
    public const string CitationEvent = "citation";
    public const string DoneEvent = "done";
    public const string ErrorEvent = "error";

    /// <summary>Sends one user message and drains the agent's stream.</summary>
    public static async Task<IReadOnlyList<AgentEvent>> ChatAsync(
        this HttpClient client, string prompt, Guid? conversationId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/assistant/chat")
        {
            Content = JsonContent.Create(new
            {
                conversationId,
                newMessage = new { role = "User", content = prompt },
            }),
        };

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();

        List<AgentEvent> events = [];
        await foreach (var item in SseParser.Create(stream).EnumerateAsync())
            events.Add(new AgentEvent(item.EventType, JsonDocument.Parse(item.Data).RootElement.Clone()));

        return events;
    }

    /// <summary>Total tickets in the workspace, used to assert what a scenario changed.</summary>
    public static async Task<int> CountTicketsAsync(this HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/tickets?pageSize=1");
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("totalCount").GetInt32();
    }

    public static async Task<Guid> FindTicketIdAsync(this HttpClient client, string title)
    {
        using var response = await client.GetAsync($"/api/v1/tickets?searchTerm={Uri.EscapeDataString(title)}");
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid();
    }

    public static async Task<IReadOnlyList<JsonElement>> AuditEventsAsync(this HttpClient client, Guid ticketId)
    {
        using var response = await client.GetAsync($"/api/v1/tickets/{ticketId}/audit-events");
        response.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    /// <summary>The assistant's visible answer, reassembled from its text deltas.</summary>
    public static string AnswerOf(this IReadOnlyList<AgentEvent> events)
        => string.Concat(events.Where(e => e.Type == TextEvent).Select(e => e.Text));

    public static IEnumerable<string> EventTypes(this IReadOnlyList<AgentEvent> events)
        => events.Select(e => e.Type);

    public static IEnumerable<AgentEvent> OfType(this IReadOnlyList<AgentEvent> events, string type)
        => events.Where(e => e.Type == type);
}
