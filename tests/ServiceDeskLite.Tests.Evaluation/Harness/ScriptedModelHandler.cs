using System.Net;
using System.Text;
using System.Text.Json;

namespace ServiceDeskLite.Tests.Evaluation.Harness;

/// <summary>
/// Serves the scripted turns in order, and records every request the agent made.
/// </summary>
/// <remarks>
/// Recording matters as much as replaying: the second request carries the tool_result the agent
/// fed back to the model, and that round trip — did the handler's failure reach the model, and in
/// what shape — is the part of the loop no unit test sees.
/// </remarks>
public sealed class ScriptedModelHandler : HttpMessageHandler
{
    private readonly Queue<ScriptedTurn> _turns = new();
    private readonly List<JsonDocument> _requests = [];
    private HttpStatusCode? _failWith;
    private string _failBody = string.Empty;

    /// <summary>Every request body the agent sent, oldest first.</summary>
    public IReadOnlyList<JsonElement> Requests => _requests.Select(d => d.RootElement).ToList();

    public int RequestCount => _requests.Count;

    public ScriptedModelHandler Then(ScriptedTurn turn)
    {
        _turns.Enqueue(turn);
        return this;
    }

    /// <summary>Makes the upstream API fail with a well-formed API error, as it does in production.</summary>
    public ScriptedModelHandler FailsWith(HttpStatusCode status)
    {
        _failWith = status;
        _failBody = """{"type":"error","error":{"type":"api_error","message":"Internal server error"}}""";
        return this;
    }

    /// <summary>
    /// Makes the upstream return a body the SDK cannot make sense of. Nothing guarantees an
    /// upstream response is shaped as documented, and the adapter promises the stream never breaks.
    /// </summary>
    public ScriptedModelHandler RespondsWithGarbage()
    {
        _failWith = HttpStatusCode.OK;
        _failBody = "this is not an event stream";
        return this;
    }

    /// <summary>Tool inputs the agent sent for <paramref name="toolName"/>, across all turns.</summary>
    public IReadOnlyList<JsonElement> ToolResultsFor(string toolUseId) =>
        Requests
            .SelectMany(r => r.GetProperty("messages").EnumerateArray())
            .Where(m => m.GetProperty("role").GetString() == "user")
            .SelectMany(m => m.GetProperty("content").ValueKind == JsonValueKind.Array
                ? m.GetProperty("content").EnumerateArray()
                : [])
            .Where(block => block.ValueKind == JsonValueKind.Object
                && block.TryGetProperty("tool_use_id", out var id)
                && id.GetString() == toolUseId)
            .ToList();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is { } content)
            _requests.Add(JsonDocument.Parse(await content.ReadAsStringAsync(cancellationToken)));

        if (_failWith is { } status)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(_failBody, Encoding.UTF8, "application/json"),
            };
        }

        if (_turns.Count == 0)
        {
            throw new InvalidOperationException(
                $"The agent made {_requests.Count} model requests, but only {_requests.Count - 1} were scripted. "
                + "An unscripted request means the loop ran a turn the scenario did not expect.");
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_turns.Dequeue().ToSse(), Encoding.UTF8, "text/event-stream"),
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var request in _requests)
                request.Dispose();
        }

        base.Dispose(disposing);
    }
}
