using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Application.Assistant.GetAiDashboard;

/// <summary>
/// Turns raw tool invocations into the dashboard's retrieval and per-tool figures.
/// Lives here rather than in either persistence adapter: both providers must produce the
/// same numbers from the same rows, and duplicating this arithmetic is exactly how they
/// would stop doing so. The adapters only fetch the window; the meaning is defined once.
/// </summary>
public static class AiDashboardAggregation
{
    public static RetrievalDto Retrieval(IReadOnlyList<AssistantToolInvocation> invocations)
    {
        var duplicateChecks = invocations.Where(i => i.Kind == AssistantToolKind.DuplicateCheck).ToList();

        // Only retrieval confidence, and only where it was actually measured. A keyword-only
        // retrieval finds matches yet reports no score, and an evaluation tool's score grades
        // an answer rather than ranking evidence — neither belongs in this average.
        var scores = invocations
            .Where(i => i.Kind is AssistantToolKind.Retrieval or AssistantToolKind.DuplicateCheck)
            .Where(i => i.Confidence is not null)
            .Select(i => i.Confidence!.Value)
            .ToList();

        return new RetrievalDto(
            DuplicateChecks: duplicateChecks.Count,
            DuplicateChecksWithMatch: duplicateChecks.Count(i => i.MatchCount > 0),
            ConfidenceSamples: scores.Count,
            AverageConfidence: scores.Count == 0 ? null : scores.Average(),
            SemanticAvailable: invocations.Any(i => i.SemanticAvailable == true));
    }

    public static IReadOnlyList<ToolUsageDto> Tools(IReadOnlyList<AssistantToolInvocation> invocations) =>
        invocations
            .GroupBy(i => i.ToolName, StringComparer.Ordinal)
            .Select(g =>
            {
                var scores = g.Where(i => i.Confidence is not null).Select(i => i.Confidence!.Value).ToList();

                return new ToolUsageDto(
                    ToolName: g.Key,
                    Invocations: g.Count(),
                    Errors: g.Count(i => i.IsError),
                    AverageConfidence: scores.Count == 0 ? null : scores.Average());
            })
            .OrderByDescending(t => t.Invocations)
            .ThenBy(t => t.ToolName, StringComparer.Ordinal)
            .ToList();
}
