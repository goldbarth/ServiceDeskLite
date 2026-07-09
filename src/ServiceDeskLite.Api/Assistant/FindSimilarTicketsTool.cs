using System.Globalization;
using System.Text;
using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Hybrid RAG retrieval exposed as a tool: the model decides when to search, and the
/// query is answered by <see cref="IHybridTicketSearch"/>, which blends semantic
/// (Voyage + pgvector cosine) and keyword signals with Reciprocal Rank Fusion,
/// constrained by optional status/priority metadata filters (ADR-0030). Results carry
/// a fused relevance score so the model can judge confidence, and each is labelled
/// with the signals that matched it. When the semantic signal is unavailable (no
/// Voyage key / InMemory) the blend degrades to keyword-only and says so, rather than
/// pretending semantic evidence.
/// </summary>
public sealed partial class FindSimilarTicketsTool
{
    public const string Name = "find_similar_tickets";

    private const int DefaultLimit = 5;
    private const int MaxLimit = 10;

    private readonly IHybridTicketSearch _search;
    private readonly ILogger<FindSimilarTicketsTool> _logger;

    public FindSimilarTicketsTool(IHybridTicketSearch search, ILogger<FindSimilarTicketsTool> logger)
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
                ["statuses"] = JsonSerializer.SerializeToElement(new
                {
                    type = "array",
                    items = new { type = "string", @enum = Enum.GetNames<TicketStatus>() },
                    description = StatusesDescription,
                }),
                ["priorities"] = JsonSerializer.SerializeToElement(new
                {
                    type = "array",
                    items = new { type = "string", @enum = Enum.GetNames<TicketPriority>() },
                    description = PrioritiesDescription,
                }),
            },
            Required = ["query"],
        },
    };

    /// <summary>Maps and validates tool input. Static and side-effect free for unit testing.</summary>
    public static bool TryParseInput(JsonElement input, out HybridTicketSearchQuery query, out string? error)
    {
        query = null!;

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

        var limit = DefaultLimit;
        if (input.TryGetProperty("limit", out var limitEl) && limitEl.ValueKind is not JsonValueKind.Null)
        {
            if (limitEl.ValueKind is not JsonValueKind.Number
                || !limitEl.TryGetInt32(out limit)
                || limit is < 1 or > MaxLimit)
            {
                error = $"Property 'limit' must be an integer between 1 and {MaxLimit}.";
                return false;
            }
        }

        if (!TryParseEnums<TicketStatus>(input, "statuses", out var statuses, out error))
            return false;
        if (!TryParseEnums<TicketPriority>(input, "priorities", out var priorities, out error))
            return false;

        query = new HybridTicketSearchQuery(queryEl.GetString()!.Trim(), limit, statuses, priorities);
        error = null;
        return true;
    }

    private static bool TryParseEnums<TEnum>(
        JsonElement input, string property, out IReadOnlyCollection<TEnum>? values, out string? error)
        where TEnum : struct, Enum
    {
        values = null;
        error = null;

        if (!input.TryGetProperty(property, out var el) || el.ValueKind is JsonValueKind.Null)
            return true;

        if (el.ValueKind is not JsonValueKind.Array)
        {
            error = $"Property '{property}' must be an array of strings.";
            return false;
        }

        var parsed = new List<TEnum>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind is not JsonValueKind.String
                || !Enum.TryParse<TEnum>(item.GetString(), ignoreCase: true, out var value)
                || !Enum.IsDefined(value))
            {
                error = $"Property '{property}' contains an invalid {typeof(TEnum).Name}: '{item}'. "
                    + $"Valid values: {string.Join(", ", Enum.GetNames<TEnum>())}.";
                return false;
            }

            parsed.Add(value);
        }

        values = parsed.Count > 0 ? parsed : null;
        return true;
    }

    /// <summary>Formats fused matches for the model: one line per ticket with relevance and matched signals.</summary>
    public static string FormatResult(HybridTicketSearchResult result, string query)
    {
        if (result.Matches.Count == 0)
        {
            return result.SemanticAvailable
                ? $"No tickets similar to \"{query}\" were found."
                : $"Semantic search is unavailable here; no tickets matched \"{query}\" by keyword either.";
        }

        var sb = new StringBuilder();
        var mode = result.SemanticAvailable
            ? "hybrid semantic + keyword search"
            : "keyword-only search (semantic search unavailable — treat as weaker evidence)";

        sb.Append(CultureInfo.InvariantCulture,
            $"Found {result.Matches.Count} ticket(s) for \"{query}\" via {mode}, best first:");

        foreach (var m in result.Matches)
        {
            var signals = (m.FromSemantic, m.FromKeyword) switch
            {
                (true, true) => "semantic+keyword",
                (true, false) => "semantic",
                _ => "keyword",
            };
            var similarity = m.Similarity is { } s
                ? string.Create(CultureInfo.InvariantCulture, $", similarity={s:P0}")
                : string.Empty;

            sb.AppendLine();
            sb.Append(CultureInfo.InvariantCulture,
                $"- id={m.Id.Value} | \"{m.Title}\" | status={m.Status} | priority={m.Priority} | relevance={m.Relevance:P0}{similarity} | matched={signals}");
        }

        return sb.ToString();
    }

    /// <summary>Confidence proxy: the strongest match's fused relevance (0 when there are none).</summary>
    public static double TopRelevance(HybridTicketSearchResult result) =>
        result.Matches.Count > 0 ? result.Matches.Max(m => m.Relevance) : 0.0;

    public async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct)
    {
        if (!TryParseInput(input, out var query, out var parseError))
            return new ToolResult($"Invalid tool input: {parseError}", true);

        HybridTicketSearchResult result;
        try
        {
            result = await _search.SearchAsync(query, ct);
        }
        catch (Exception ex) when (TransientFault.IsTransient(ex, ct))
        {
            // Let the retry policy handle transient faults (e.g. Voyage 429/5xx).
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Hybrid ticket search failed for assistant query");
            return new ToolResult("Ticket search failed due to a technical error. Continue without it.", true);
        }

        // No vector score when semantic did not run; report null confidence rather than a fake one.
        double? confidence = result.SemanticAvailable && result.Matches.Count > 0
            ? TopRelevance(result)
            : null;

        return new ToolResult(
            FormatResult(result, query.Text),
            IsError: false,
            Confidence: confidence,
            MatchCount: result.Matches.Count,
            SemanticAvailable: result.SemanticAvailable);
    }
}
