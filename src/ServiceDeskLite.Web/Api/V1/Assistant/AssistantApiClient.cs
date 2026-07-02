using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;

using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Web.Api.V1.Assistant;

/// <summary>
/// Consumes the assistant SSE endpoint. Uses ResponseHeadersRead so events are
/// surfaced as they arrive instead of after the full response is buffered.
/// </summary>
internal sealed class AssistantApiClient(HttpClient http) : IAssistantApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    public async IAsyncEnumerable<AssistantStreamEvent> ChatAsync(
        IReadOnlyList<AssistantChatMessage> transcript,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/assistant/chat")
        {
            Content = JsonContent.Create(new AssistantChatRequest(transcript)),
        };
        request.Headers.Accept.Add(new("text/event-stream"));

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadProblemDetailAsync(response, ct);
            yield return new AssistantStreamEvent(AssistantStreamEvent.ErrorEvent, Message: detail);
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        await foreach (var sse in SseParser.Create(stream).EnumerateAsync(ct))
        {
            var payload = Deserialize(sse.Data);
            yield return new AssistantStreamEvent(
                sse.EventType,
                payload?.Text,
                payload?.ToolName,
                payload?.TicketId,
                payload?.IsError,
                payload?.Message);

            if (sse.EventType is AssistantStreamEvent.DoneEvent or AssistantStreamEvent.ErrorEvent)
                yield break;
        }
    }

    private static Payload? Deserialize(string data)
    {
        try
        {
            return JsonSerializer.Deserialize<Payload>(data, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(ct);
            return problem?.Detail ?? problem?.Title ?? $"Request failed ({(int)response.StatusCode}).";
        }
        catch (JsonException)
        {
            return $"Request failed ({(int)response.StatusCode}).";
        }
    }

    private sealed record Payload(
        string? Text,
        string? ToolName,
        Guid? TicketId,
        bool? IsError,
        string? Message);
}
