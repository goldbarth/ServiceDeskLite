namespace ServiceDeskLite.Contracts.V1.Dashboard;

/// <summary>
/// AI operations over a trailing window of <paramref name="WindowDays"/> days.
/// Rates are nullable throughout: absent means "no denominator", which is not the same
/// as zero, and a client must render the difference.
/// </summary>
public sealed record AiDashboardResponse(
    int WindowDays,
    TicketVolumeResponse Volume,
    AutomationResponse Automation,
    RetrievalResponse Retrieval,
    IReadOnlyList<ToolUsageResponse> Tools,
    TokenUsageResponse Tokens);

public sealed record TicketVolumeResponse(int TotalTickets, int CreatedInWindow);

/// <param name="Rate">Share of audited actions authored by the assistant; null when nothing happened.</param>
public sealed record AutomationResponse(int AiActions, int TotalActions, double? Rate);

/// <param name="DuplicateRate">Share of duplicate checks that surfaced a candidate; null when none ran.</param>
/// <param name="AverageConfidence">Mean top-match relevance; null when nothing reported a score.</param>
/// <param name="SemanticAvailable">
/// False means semantic retrieval is not configured in this deployment, so confidence is
/// not measurable — distinct from a measured low score.
/// </param>
public sealed record RetrievalResponse(
    int DuplicateChecks,
    int DuplicateChecksWithMatch,
    double? DuplicateRate,
    int ConfidenceSamples,
    double? AverageConfidence,
    bool SemanticAvailable);

public sealed record ToolUsageResponse(
    string ToolName,
    int Invocations,
    int Errors,
    double? ErrorRate,
    double? AverageConfidence);

/// <param name="ModelTurns">Model round trips billed in the window. One chat turn can span several.</param>
public sealed record TokenUsageResponse(
    int ModelTurns,
    long InputTokens,
    long OutputTokens,
    long TotalTokens);
