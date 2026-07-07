using System.Globalization;
using System.Text;
using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Abstractions.Search;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// RAG retrieval step exposed as a tool: the model decides when to search,
/// the query is embedded (Voyage) and matched against ticket embeddings in
/// Postgres (pgvector, cosine). Results go back as a tool_result so the model
/// can ground its answer in existing tickets — e.g. to flag duplicates before
/// creating a new one.
/// </summary>
public sealed partial class FindSimilarTicketsTool
{
    public const string Name = "find_similar_tickets";

    private const int DefaultLimit = 5;
    private const int MaxLimit = 10;

    private readonly ITicketSimilaritySearch _search;
    private readonly ILogger<FindSimilarTicketsTool> _logger;

    public FindSimilarTicketsTool(ITicketSimilaritySearch search, ILogger<FindSimilarTicketsTool> logger)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static Tool Definition => new()
    {
        Name = Name,
        Description = ToolDescription,
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["query"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = QueryDescription,
                }),
                ["limit"] = JsonSerializer.SerializeToElement(new
                {
                    type = "integer",
                    minimum = 1,
                    maximum = MaxLimit,
                    description = LimitDescription,
                }),
            },
            Required = ["query"],
        },
    };

    /// <summary>Maps and validates tool input. Static and side-effect free for unit testing.</summary>
    public static bool TryParseInput(JsonElement input, out string query, out int limit, out string? error)
    {
        query = string.Empty;
        limit = DefaultLimit;

        if (input.ValueKind is not JsonValueKind.Object)
        {
            error = "Tool input must be a JSON object.";
            return false;
        }

        if (!input.TryGetProperty("query", out var queryEl)
            || queryEl.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(queryEl.GetString()))
        {
            error = "Missing or invalid required string property 'query'.";
            return false;
        }

        if (input.TryGetProperty("limit", out var limitEl) && limitEl.ValueKind is not JsonValueKind.Null)
        {
            if (limitEl.ValueKind is not JsonValueKind.Number
                || !limitEl.TryGetInt32(out var parsedLimit)
                || parsedLimit is < 1 or > MaxLimit)
            {
                error = $"Property 'limit' must be an integer between 1 and {MaxLimit}.";
                return false;
            }

            limit = parsedLimit;
        }

        query = queryEl.GetString()!;
        error = null;
        return true;
    }

    /// <summary>Formats matches for the model: compact, one line per ticket, similarity as percentage.</summary>
    public static string FormatResult(string query, IReadOnlyList<TicketSimilarityMatch> matches)
    {
        if (matches.Count == 0)
            return $"No tickets similar to \"{query}\" were found.";

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Found {matches.Count} ticket(s) similar to \"{query}\":");

        foreach (var m in matches)
        {
            sb.AppendLine();
            sb.Append(CultureInfo.InvariantCulture,
                $"- id={m.Id.Value} | \"{m.Title}\" | status={m.Status} | priority={m.Priority} | similarity={m.Similarity:P0}");
        }

        return sb.ToString();
    }

    public async Task<(string Content, bool IsError, Guid? TicketId)> ExecuteAsync(
        JsonElement input,
        CancellationToken ct)
    {
        if (!TryParseInput(input, out var query, out var limit, out var parseError))
            return ($"Invalid tool input: {parseError}", true, null);

        TicketSimilaritySearchResult result;
        try
        {
            result = await _search.SearchAsync(query, limit, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Similarity search failed for assistant query");
            return ("Similarity search failed due to a technical error. Continue without it.", true, null);
        }

        if (!result.IsAvailable)
            return ("Semantic ticket search is not available in this deployment. Continue without it " +
                    "and do not retry this tool.", false, null);

        return (FormatResult(query, result.Matches), false, null);
    }
}
