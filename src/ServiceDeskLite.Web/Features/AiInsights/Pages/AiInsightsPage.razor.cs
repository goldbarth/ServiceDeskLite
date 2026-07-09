using Microsoft.AspNetCore.Components;

using ServiceDeskLite.Contracts.V1.Dashboard;
using ServiceDeskLite.Web.Api.V1;
using ServiceDeskLite.Web.Features.Dashboard.Components;

namespace ServiceDeskLite.Web.Features.AiInsights.Pages;

public partial class AiInsightsPage
{
    [Inject] private ITicketsApiClient TicketsApi { get; set; } = default!;

    private bool _isLoading;
    private ApiError? _error;
    private AiDashboardResponse? _metrics;

    /// <summary>
    /// Whether the assistant produced anything in the window. Drives a single up-front
    /// notice instead of six cards each explaining their own emptiness.
    /// </summary>
    private bool HasAssistantActivity
        => _metrics is not null && (_metrics.Tokens.ModelTurns > 0 || _metrics.Tools.Count > 0);

    private int TotalToolCalls => _metrics?.Tools.Sum(t => t.Invocations) ?? 0;

    private int TotalToolErrors => _metrics?.Tools.Sum(t => t.Errors) ?? 0;

    private string HeroDescription
        => $"Automation, retrieval quality and model cost across the last {_metrics?.WindowDays ?? 0} days.";

    private IReadOnlyList<DashboardHeroStat> HeroStats
        => _metrics is null
            ? []
            :
            [
                new("Automation", Format.Percent(_metrics.Automation.Rate)),
                new("Model turns", Format.Count(_metrics.Tokens.ModelTurns)),
                new("Tokens", Format.Tokens(_metrics.Tokens.TotalTokens))
            ];

    private string VolumeSupport
        => _metrics is null
            ? string.Empty
            : $"{Format.Count(_metrics.Volume.CreatedInWindow)} created in the last {_metrics.WindowDays} days";

    private string AutomationSupport
        => _metrics is null || _metrics.Automation.TotalActions == 0
            ? "No ticket activity in this window"
            : $"{Format.Count(_metrics.Automation.AiActions)} of "
              + $"{Format.Count(_metrics.Automation.TotalActions)} audited actions by the assistant";

    private string DuplicateSupport
        => _metrics is null || _metrics.Retrieval.DuplicateChecks == 0
            ? "No duplicate checks ran in this window"
            : $"{Format.Count(_metrics.Retrieval.DuplicateChecksWithMatch)} of "
              + $"{Format.Count(_metrics.Retrieval.DuplicateChecks)} checks surfaced a candidate";

    // Three distinct states, and collapsing any two of them would misinform: semantic
    // retrieval is not configured; it is configured but nothing scored yet; it scored.
    private string RagConfidenceValue
        => _metrics is null || !_metrics.Retrieval.SemanticAvailable
            ? Format.NoValue
            : Format.Percent(_metrics.Retrieval.AverageConfidence);

    private string? RagConfidenceChip
        => _metrics is not null && !_metrics.Retrieval.SemanticAvailable ? "Unavailable" : null;

    /// <summary>A green card reads as a good score; an unmeasured one has no score to be good.</summary>
    private string RagConfidenceAccent
        => _metrics is not null && _metrics.Retrieval.SemanticAvailable ? "green" : "slate";

    private string RagConfidenceSupport
    {
        get
        {
            if (_metrics is null)
                return string.Empty;

            if (!_metrics.Retrieval.SemanticAvailable)
                return "Semantic search is not configured here, so confidence is not measured";

            return _metrics.Retrieval.ConfidenceSamples == 0
                ? "No retrieval reported a score in this window"
                : $"Mean top-match relevance over {Format.Count(_metrics.Retrieval.ConfidenceSamples)} retrievals";
        }
    }

    private string ToolCallsSupport
        => TotalToolCalls == 0
            ? "No tool was called in this window"
            : $"{Format.Count(TotalToolErrors)} returned an error";

    private string TokensSupport
        => _metrics is null || _metrics.Tokens.ModelTurns == 0
            ? "No model turns in this window"
            : $"{Format.Tokens(_metrics.Tokens.InputTokens)} in, "
              + $"{Format.Tokens(_metrics.Tokens.OutputTokens)} out over "
              + $"{Format.Count(_metrics.Tokens.ModelTurns)} model turns";

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;

        try
        {
            var result = await TicketsApi.GetAiDashboardAsync();

            if (result.IsSuccess)
                _metrics = result.Value;
            else
                _error = result.Error;
        }
        finally
        {
            _isLoading = false;
        }
    }
}
