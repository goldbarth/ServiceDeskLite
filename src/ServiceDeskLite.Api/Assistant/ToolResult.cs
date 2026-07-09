using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// The outcome of one assistant tool invocation. Replaces the growing return tuple
/// so new signals (here: knowledge-base <see cref="Citations"/>) can be added without
/// touching every tool — the existing tools keep returning their 4-value tuple, which
/// converts implicitly. <see cref="Content"/> goes back to the model as the tool_result;
/// the rest drives SSE events (tool_result, citation) to the client and the AI dashboard's
/// metrics (<see cref="MatchCount"/>, <see cref="SemanticAvailable"/>).
/// </summary>
/// <param name="MatchCount">
/// Results a retrieval tool returned; null for tools that retrieve nothing. Distinct from
/// <paramref name="Confidence"/>, which a keyword-only retrieval withholds even when it
/// does find matches.
/// </param>
/// <param name="SemanticAvailable">
/// Whether a retrieval's semantic half ran; null for non-retrieval tools.
/// </param>
public readonly record struct ToolResult(
    string Content,
    bool IsError,
    Guid? TicketId = null,
    double? Confidence = null,
    IReadOnlyList<AssistantCitation>? Citations = null,
    int? MatchCount = null,
    bool? SemanticAvailable = null)
{
    public static implicit operator ToolResult(
        (string Content, bool IsError, Guid? TicketId, double? Confidence) t) =>
        new(t.Content, t.IsError, t.TicketId, t.Confidence);

    /// <summary>Back-compat deconstruction for callers/tests that only read the first four values.</summary>
    public void Deconstruct(out string content, out bool isError, out Guid? ticketId, out double? confidence)
    {
        content = Content;
        isError = IsError;
        ticketId = TicketId;
        confidence = Confidence;
    }
}
