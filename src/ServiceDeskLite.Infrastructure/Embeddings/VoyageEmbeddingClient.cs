using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

namespace ServiceDeskLite.Infrastructure.Embeddings;

/// <summary>
/// Minimal REST client for the Voyage AI embeddings endpoint (no official
/// .NET SDK exists). Auth and base address are configured on the typed
/// HttpClient in DI; this class only shapes the request/response.
/// </summary>
public sealed class VoyageEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _http;
    private readonly VoyageOptions _options;

    public VoyageEmbeddingClient(HttpClient http, IOptions<VoyageOptions> options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        EmbeddingInputType inputType,
        CancellationToken ct)
    {
        if (inputs.Count == 0)
            return [];

        var request = new EmbeddingRequest(
            Input: inputs,
            Model: _options.Model,
            InputType: inputType == EmbeddingInputType.Query ? "query" : "document",
            OutputDimension: _options.Dimensions);

        using var response = await _http.PostAsJsonAsync("embeddings", request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(ct)
                      ?? throw new InvalidOperationException("Voyage API returned an empty body.");

        if (payload.Data.Count != inputs.Count)
            throw new InvalidOperationException(
                $"Voyage API returned {payload.Data.Count} embeddings for {inputs.Count} inputs.");

        // The API documents index-annotated results; sort defensively instead of
        // trusting response order.
        return payload.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToList();
    }

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input_type")] string InputType,
        [property: JsonPropertyName("output_dimension")] int OutputDimension);

    private sealed record EmbeddingResponse(
        [property: JsonPropertyName("data")] List<EmbeddingDatum> Data);

    private sealed record EmbeddingDatum(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] float[] Embedding);
}
