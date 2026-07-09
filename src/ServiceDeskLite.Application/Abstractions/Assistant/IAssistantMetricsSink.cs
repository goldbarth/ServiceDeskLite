namespace ServiceDeskLite.Application.Abstractions.Assistant;

/// <summary>
/// Append-only capture of what the assistant did, so the AI dashboard can report on it.
/// Nothing else reads these records, and no behaviour depends on them — a write that
/// fails must never take a chat turn down with it (ADR-0034).
/// </summary>
/// <remarks>
/// The edge owns the vocabulary here: tool names and model ids come from the Anthropic
/// adapter, and the sink stores them as opaque strings. That keeps the inward-dependency
/// rule intact, exactly as <see cref="IConversationStore"/> does for message content.
/// </remarks>
public interface IAssistantMetricsSink
{
    /// <summary>Records one completed tool invocation, successful or not.</summary>
    Task RecordToolInvocationAsync(AssistantToolInvocation invocation, CancellationToken ct);

    /// <summary>Records the token cost of one model turn, reported by the API at the end of the turn.</summary>
    Task RecordTokenUsageAsync(AssistantTokenUsage usage, CancellationToken ct);
}

/// <summary>
/// What a tool call was for, as classified by the edge that owns the tool.
/// Reporting aggregates on this rather than on tool names, so the dashboard never has to
/// recognise a string like <c>find_similar_tickets</c> that belongs to the Anthropic adapter.
/// Renaming a tool then stays an edge concern.
/// </summary>
public enum AssistantToolKind
{
    /// <summary>Changes a ticket through a command handler.</summary>
    Action,

    /// <summary>Reads or retrieves; a confidence score here means top-match relevance.</summary>
    Retrieval,

    /// <summary>Retrieval whose purpose is to find an existing ticket before a new one is created.</summary>
    DuplicateCheck,

    /// <summary>
    /// Scores something the model produced, rather than fetching evidence. Its confidence is a
    /// grounding score, not a retrieval relevance, so it is kept out of the retrieval average —
    /// the two numbers measure different things and mean nothing when blended.
    /// </summary>
    Evaluation,
}

/// <param name="ToolName">Tool as named to the model, e.g. <c>find_similar_tickets</c>. Reported per tool, never matched on.</param>
/// <param name="Kind">What the call was for; the dashboard aggregates on this.</param>
/// <param name="IsError">The tool returned an error result to the model.</param>
/// <param name="Confidence">
/// Top-match relevance for retrieval tools, absent for tools that carry no such signal —
/// and deliberately absent when a retrieval ran without its semantic half, so an average
/// never blends measured confidence with keyword-only guesses.
/// </param>
/// <param name="MatchCount">
/// Number of results a retrieval tool returned, absent for non-retrieval tools. Separate
/// from <paramref name="Confidence"/> because a keyword-only search still finds matches
/// while reporting no confidence.
/// </param>
/// <param name="SemanticAvailable">
/// Whether the semantic half of a retrieval ran. Absent for non-retrieval tools. Lets the
/// dashboard say "measured on keyword-only evidence" instead of implying a full signal.
/// </param>
public sealed record AssistantToolInvocation(
    string ToolName,
    AssistantToolKind Kind,
    bool IsError,
    double? Confidence,
    int? MatchCount,
    bool? SemanticAvailable,
    DateTimeOffset OccurredAt);

/// <param name="Model">Anthropic model id that served the turn.</param>
/// <param name="InputTokens">Tokens consumed by the prompt, as reported for this turn.</param>
/// <param name="OutputTokens">Tokens generated, as reported for this turn.</param>
public sealed record AssistantTokenUsage(
    string Model,
    long InputTokens,
    long OutputTokens,
    DateTimeOffset OccurredAt);
