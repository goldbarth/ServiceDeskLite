using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;

using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Web.Api.V1.Assistant;

/// <summary>
/// Consumes the ticket-summary SSE endpoint. Uses ResponseHeadersRead so sections
/// surface as they arrive instead of after the full response is buffered.
/// </summary>
public sealed class TicketSummaryApiClient(HttpClient http) : ITicketSummaryApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    public async IAsyncEnumerable<TicketSummaryStreamEvent> StreamAsync(
        Guid ticketId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/tickets/{ticketId}/summary");
        request.Headers.Accept.Add(new("text/event-stream"));

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadProblemDetailAsync(response, ct);
            yield return new TicketSummaryStreamEvent(TicketSummaryStreamEvent.ErrorEvent, Message: detail);
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        await foreach (var sse in SseParser.Create(stream).EnumerateAsync(ct))
        {
            var payload = Deserialize(sse.Data);
            yield return new TicketSummaryStreamEvent(
                sse.EventType, payload?.Section, payload?.Text, payload?.Message);

            if (sse.EventType is TicketSummaryStreamEvent.DoneEvent or TicketSummaryStreamEvent.ErrorEvent)
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

    private sealed record Payload(TicketSummarySection? Section, string? Text, string? Message);
}
