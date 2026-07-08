using System.Globalization;
using System.Text;
using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Long-term memory recall exposed as a tool: the model decides when to look up
/// what it knows about the user. The query is embedded and matched by cosine
/// similarity against the owner's stored memories, so recall works across
/// conversations. Mirrors <see cref="FindSimilarTicketsTool"/>; degrades honestly
/// where no embedding store is available.
/// </summary>
public sealed partial class RecallMemoryTool
{
    public const string Name = "recall_memory";

    private const int DefaultLimit = 5;
    private const int MaxLimit = 10;

    private readonly IMemorySearch _memory;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<RecallMemoryTool> _logger;

    public RecallMemoryTool(IMemorySearch memory, ICurrentUser currentUser, ILogger<RecallMemoryTool> logger)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
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

    /// <summary>Formats matches for the model: compact, one line per memory, similarity as percentage.</summary>
    public static string FormatResult(string query, IReadOnlyList<MemoryMatch> matches)
    {
        if (matches.Count == 0)
            return $"No stored memories relevant to \"{query}\" were found.";

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Found {matches.Count} memory/memories relevant to \"{query}\":");

        foreach (var m in matches)
        {
            sb.AppendLine();
            sb.Append(CultureInfo.InvariantCulture, $"- [{m.Kind}] {m.Content} (relevance={m.Similarity:P0})");
        }

        return sb.ToString();
    }

    /// <summary>Confidence proxy: the strongest match's similarity (0 when there are none).</summary>
    public static double TopSimilarity(IReadOnlyList<MemoryMatch> matches) =>
        matches.Count > 0 ? matches.Max(m => m.Similarity) : 0.0;

    public async Task<(string Content, bool IsError, Guid? TicketId, double? Confidence)> ExecuteAsync(
        JsonElement input,
        CancellationToken ct)
    {
        if (!TryParseInput(input, out var query, out var limit, out var parseError))
            return ($"Invalid tool input: {parseError}", true, null, null);

        MemorySearchResult result;
        try
        {
            result = await _memory.SearchAsync(_currentUser.Owner, query, limit, ct);
        }
        catch (Exception ex) when (TransientFault.IsTransient(ex, ct))
        {
            // Let the retry policy handle transient faults (e.g. Voyage 429/5xx).
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Memory recall failed for assistant query");
            return ("Memory recall failed due to a technical error. Continue without it.", true, null, null);
        }

        if (!result.IsAvailable)
            return ("Long-term memory is not available in this deployment. Continue without it " +
                    "and do not retry this tool.", false, null, null);

        return (FormatResult(query, result.Matches), false, null, TopSimilarity(result.Matches));
    }
}
