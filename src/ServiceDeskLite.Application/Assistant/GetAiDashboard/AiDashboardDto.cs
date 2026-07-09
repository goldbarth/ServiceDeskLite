namespace ServiceDeskLite.Application.Assistant.GetAiDashboard;

/// <summary>
/// AI operations over a trailing window. Every rate is nullable, and that is the point:
/// a rate with no denominator is not zero, it is unknown. Rendering "0 %" for a system
/// nobody has used yet would be a lie the UI cannot walk back (ADR-0034).
/// </summary>
/// <param name="WindowDays">Length of the trailing window the windowed figures cover.</param>
public sealed record AiDashboardDto(
    int WindowDays,
    TicketVolumeDto Volume,
    AutomationDto Automation,
    RetrievalDto Retrieval,
    IReadOnlyList<ToolUsageDto> Tools,
    TokenUsageDto Tokens);

/// <param name="TotalTickets">All tickets, regardless of age.</param>
/// <param name="CreatedInWindow">Tickets created within the window.</param>
public sealed record TicketVolumeDto(int TotalTickets, int CreatedInWindow);

/// <summary>
/// How much of the ticket work was done by the assistant rather than by a human.
/// Measured on audit events, which every write path produces — the assistant reaches the
/// domain only through the same command handlers, so its actions are audited like anyone
/// else's, distinguished by actor alone.
/// </summary>
public sealed record AutomationDto(int AiActions, int TotalActions)
{
    /// <summary>Share of audited actions authored by the assistant; null when nothing happened in the window.</summary>
    public double? Rate => TotalActions == 0 ? null : (double)AiActions / TotalActions;
}

/// <summary>
/// Duplicate detection and retrieval confidence.
/// </summary>
/// <param name="DuplicateChecks">Invocations of the duplicate-check tool in the window.</param>
/// <param name="DuplicateChecksWithMatch">Those that surfaced at least one candidate.</param>
/// <param name="ConfidenceSamples">Retrieval invocations that reported a confidence score.</param>
/// <param name="AverageConfidence">
/// Mean top-match relevance across those samples; null when none reported one. A retrieval
/// that ran without its semantic half reports no score at all, so this never averages
/// measured relevance together with keyword-only results.
/// </param>
/// <param name="SemanticAvailable">
/// Whether any retrieval in the window ran with its semantic half. False means the
/// deployment has no Voyage key or no pgvector, and confidence is simply not measurable
/// here — a distinct condition from "measured, and it was low".
/// </param>
public sealed record RetrievalDto(
    int DuplicateChecks,
    int DuplicateChecksWithMatch,
    int ConfidenceSamples,
    double? AverageConfidence,
    bool SemanticAvailable)
{
    /// <summary>Share of duplicate checks that found a candidate; null when none ran.</summary>
    public double? DuplicateRate =>
        DuplicateChecks == 0 ? null : (double)DuplicateChecksWithMatch / DuplicateChecks;
}

/// <param name="ToolName">Tool as named to the model.</param>
/// <param name="Invocations">Total calls in the window, including failed ones.</param>
/// <param name="Errors">Calls that returned an error result to the model.</param>
/// <param name="AverageConfidence">Mean reported confidence, or null for tools that report none.</param>
public sealed record ToolUsageDto(
    string ToolName,
    int Invocations,
    int Errors,
    double? AverageConfidence)
{
    /// <summary>Share of calls that failed; null when the tool was never called.</summary>
    public double? ErrorRate => Invocations == 0 ? null : (double)Errors / Invocations;
}

/// <param name="ModelTurns">Model round trips billed in the window. One chat turn can span several.</param>
public sealed record TokenUsageDto(
    int ModelTurns,
    long InputTokens,
    long OutputTokens)
{
    public long TotalTokens => InputTokens + OutputTokens;
}
