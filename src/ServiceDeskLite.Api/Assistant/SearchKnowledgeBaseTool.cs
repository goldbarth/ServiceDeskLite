using System.Globalization;
using System.Text;
using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Knowledge-base RAG retrieval exposed as a tool: the model decides when to consult
/// the corpus (articles, FAQ, internal docs); the query is embedded (Voyage) and
/// matched against knowledge chunks in Postgres (pgvector, cosine). Matches go back
/// as a tool_result so the model can ground a solution in real documentation, and the
/// retrieved sources are surfaced to the client as citations. When knowledge-base
/// search is unavailable (no Voyage key / InMemory), the tool says so plainly and
/// returns no citations — it never fabricates a source.
/// </summary>
public sealed partial class SearchKnowledgeBaseTool
{
    public const string Name = "search_knowledge_base";

    private const int DefaultLimit = 4;
    private const int MaxLimit = 8;
    private const int SnippetLength = 240;

    private readonly IKnowledgeBaseSearch _search;
    private readonly ILogger<SearchKnowledgeBaseTool> _logger;

    public SearchKnowledgeBaseTool(IKnowledgeBaseSearch search, ILogger<SearchKnowledgeBaseTool> logger)
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

    /// <summary>Formats matches for the model: one block per source, similarity as percentage.</summary>
    public static string FormatResult(string query, IReadOnlyList<KnowledgeMatch> matches)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"Found {matches.Count} knowledge-base passage(s) for \"{query}\". Ground your answer in these and cite them by title:");

        foreach (var m in matches)
        {
            sb.AppendLine();
            sb.Append(CultureInfo.InvariantCulture,
                $"- \"{m.Title}\" ({m.Source}) › {m.Heading} | similarity={m.Similarity:P0}");
            sb.AppendLine();
            sb.Append("  ").Append(Collapse(m.Snippet));
        }

        return sb.ToString();
    }

    /// <summary>Maps retrieved passages to client-facing citations with a bounded snippet.</summary>
    public static IReadOnlyList<AssistantCitation> ToCitations(IReadOnlyList<KnowledgeMatch> matches) =>
        matches
            .Select(m => new AssistantCitation(
                m.Title, m.Source, m.Heading, Truncate(Collapse(m.Snippet), SnippetLength), m.Similarity))
            .ToList();

    /// <summary>Confidence proxy: the strongest match's similarity (0 when there are none).</summary>
    public static double TopSimilarity(IReadOnlyList<KnowledgeMatch> matches) =>
        matches.Count > 0 ? matches.Max(m => m.Similarity) : 0.0;

    public async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct)
    {
        if (!TryParseInput(input, out var query, out var limit, out var parseError))
            return new ToolResult($"Invalid tool input: {parseError}", true);

        KnowledgeSearchResult result;
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
            _logger.LogError(ex, "Knowledge-base search failed for assistant query");
            return new ToolResult(
                "Knowledge-base search failed due to a technical error. Answer from general knowledge and say the knowledge base could not be reached.",
                false);
        }

        // Honest degradation: no embeddings here, so report unavailable and cite nothing
        // rather than pretend the corpus was searched.
        if (!result.IsAvailable)
            return new ToolResult(
                "The knowledge base is not available in this environment. Answer from general knowledge and tell the user no internal sources could be consulted.",
                false);

        if (result.Matches.Count == 0)
            return new ToolResult(
                $"No knowledge-base passages matched \"{query}\". Do not invent a source; tell the user nothing relevant was found.",
                false);

        return new ToolResult(
            FormatResult(query, result.Matches),
            IsError: false,
            Confidence: TopSimilarity(result.Matches),
            Citations: ToCitations(result.Matches));
    }

    private static string Collapse(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max].TrimEnd() + "…";
}
