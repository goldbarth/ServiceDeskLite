using System.Globalization;
using System.Text;
using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// RAG retrieval step exposed as a tool: the model decides when to search,
/// the query is embedded (Voyage) and matched against ticket embeddings in
/// Postgres (pgvector, cosine). Results go back as a tool_result so the model
/// can ground its answer in existing tickets - e.g. to flag duplicates before
/// creating a new one. When semantic search is unavailable (no Voyage key /
/// InMemory) or finds nothing, it falls back to keyword search so the step still
/// produces a usable, clearly-labelled result instead of stalling.
/// </summary>
public sealed partial class FindSimilarTicketsTool
{
    public const string Name = "find_similar_tickets";

    private const int DefaultLimit = 5;
    private const int MaxLimit = 10;

    private readonly ITicketSimilaritySearch _search;
    private readonly SearchTicketsHandler _keywordSearch;
    private readonly ILogger<FindSimilarTicketsTool> _logger;

    public FindSimilarTicketsTool(
        ITicketSimilaritySearch search,
        SearchTicketsHandler keywordSearch,
        ILogger<FindSimilarTicketsTool> logger)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _keywordSearch = keywordSearch ?? throw new ArgumentNullException(nameof(keywordSearch));
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

    /// <summary>Formats keyword-fallback hits, labelled so the model knows these are not semantic matches.</summary>
    public static string FormatFallback(string query, string reason, IReadOnlyList<TicketListItemDto> items)
    {
        if (items.Count == 0)
            return $"{reason} No tickets matched \"{query}\" by keyword either.";

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"{reason} Keyword-matched {items.Count} ticket(s) for \"{query}\" (keyword hits are less precise than semantic ones):");

        foreach (var t in items)
        {
            sb.AppendLine();
            sb.Append(CultureInfo.InvariantCulture,
                $"- id={t.Id.Value} | \"{t.Title}\" | status={t.Status} | priority={t.Priority}");
        }

        return sb.ToString();
    }

    /// <summary>Confidence proxy: the strongest match's similarity (0 when there are none).</summary>
    public static double TopSimilarity(IReadOnlyList<TicketSimilarityMatch> matches) =>
        matches.Count > 0 ? matches.Max(m => m.Similarity) : 0.0;

    public async Task<ToolResult> ExecuteAsync(
        JsonElement input,
        CancellationToken ct)
    {
        if (!TryParseInput(input, out var query, out var limit, out var parseError))
            return ($"Invalid tool input: {parseError}", true, null, null);

        TicketSimilaritySearchResult result;
        try
        {
            result = await _search.SearchAsync(query, limit, ct);
        }
        catch (Exception ex) when (TransientFault.IsTransient(ex, ct))
        {
            // Let the retry policy handle transient faults (e.g. Voyage 429/5xx).
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Similarity search failed for assistant query");
            return ("Similarity search failed due to a technical error. Continue without it.", true, null, null);
        }

        // Fall back to keyword search when semantic search cannot run or found nothing,
        // so the step still yields something to reason about rather than dead-ending.
        if (!result.IsAvailable)
            return await KeywordFallbackAsync(query, limit, "Semantic search is unavailable here.", ct);

        if (result.Matches.Count == 0)
            return await KeywordFallbackAsync(query, limit, "No semantic matches found.", ct);

        return (FormatResult(query, result.Matches), false, null, TopSimilarity(result.Matches));
    }

    private async Task<ToolResult> KeywordFallbackAsync(
        string query, int limit, string reason, CancellationToken ct)
    {
        var searchQuery = new SearchTicketsQuery(
            new TicketSearchCriteria(Text: query),
            new Paging(PagingPolicy.MinPage, limit));

        var keyword = await _keywordSearch.HandleAsync(searchQuery, ct);
        if (!keyword.IsSuccess)
        {
            // Keyword is the fallback itself; report honestly and let the model proceed.
            return ($"{reason} Keyword fallback also failed; continue without duplicate detection.", false, null, null);
        }

        // No vector score for keyword hits - report null confidence rather than fake one.
        return (FormatFallback(query, reason, keyword.Value!.Page.Items), false, null, null);
    }
}
