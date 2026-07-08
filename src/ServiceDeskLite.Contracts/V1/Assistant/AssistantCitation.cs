namespace ServiceDeskLite.Contracts.V1.Assistant;

/// <summary>
/// One knowledge-base source the assistant drew on, streamed to the client on the
/// <c>citation</c> SSE event so an answer can show where it came from. Emitted only
/// for sources actually retrieved — never fabricated when knowledge-base search is
/// unavailable.
/// </summary>
public sealed record AssistantCitation(
    string Title,
    string Source,
    string Heading,
    string Snippet,
    double Similarity);
