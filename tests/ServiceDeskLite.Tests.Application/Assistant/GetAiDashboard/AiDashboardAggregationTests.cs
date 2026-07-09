using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Assistant.GetAiDashboard;

namespace ServiceDeskLite.Tests.Application.Assistant.GetAiDashboard;

/// <summary>
/// The dashboard's meaning lives here: which invocations count toward which figure, and
/// when a figure has no honest value at all. Both persistence providers delegate to this,
/// so a mistake here is a mistake in every deployment.
/// </summary>
public sealed class AiDashboardAggregationTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);

    private static AssistantToolInvocation Invocation(
        string tool,
        AssistantToolKind kind,
        bool isError = false,
        double? confidence = null,
        int? matchCount = null,
        bool? semanticAvailable = null,
        double durationMs = 10)
        => new(tool, kind, isError, confidence, matchCount, semanticAvailable,
            TimeSpan.FromMilliseconds(durationMs), At);

    [Fact]
    public void Duplicate_rate_is_null_when_no_duplicate_check_ran()
    {
        var result = AiDashboardAggregation.Retrieval(
            [Invocation("create_ticket", AssistantToolKind.Action)]);

        result.DuplicateChecks.Should().Be(0);
        result.DuplicateRate.Should().BeNull("a rate without a denominator is unknown, not zero");
    }

    [Fact]
    public void Duplicate_rate_counts_checks_that_surfaced_a_candidate()
    {
        var result = AiDashboardAggregation.Retrieval(
        [
            Invocation("find_similar_tickets", AssistantToolKind.DuplicateCheck, matchCount: 3, semanticAvailable: true),
            Invocation("find_similar_tickets", AssistantToolKind.DuplicateCheck, matchCount: 0, semanticAvailable: true),
            Invocation("find_similar_tickets", AssistantToolKind.DuplicateCheck, matchCount: 1, semanticAvailable: true),
            Invocation("find_similar_tickets", AssistantToolKind.DuplicateCheck, matchCount: 0, semanticAvailable: true),
        ]);

        result.DuplicateChecks.Should().Be(4);
        result.DuplicateChecksWithMatch.Should().Be(2);
        result.DuplicateRate.Should().Be(0.5);
    }

    [Fact]
    public void A_keyword_only_check_still_counts_as_a_match_though_it_reports_no_confidence()
    {
        // Semantic search unavailable: the tool withholds a score but does find tickets.
        // Deriving "found something" from the confidence field would lose this hit.
        var result = AiDashboardAggregation.Retrieval(
        [
            Invocation("find_similar_tickets", AssistantToolKind.DuplicateCheck,
                confidence: null, matchCount: 2, semanticAvailable: false),
        ]);

        result.DuplicateRate.Should().Be(1.0);
        result.ConfidenceSamples.Should().Be(0);
        result.AverageConfidence.Should().BeNull();
        result.SemanticAvailable.Should().BeFalse();
    }

    [Fact]
    public void Semantic_is_reported_available_when_any_retrieval_used_it()
    {
        var result = AiDashboardAggregation.Retrieval(
        [
            Invocation("find_similar_tickets", AssistantToolKind.DuplicateCheck, matchCount: 0, semanticAvailable: false),
            Invocation("search_knowledge_base", AssistantToolKind.Retrieval, confidence: 0.8, matchCount: 1, semanticAvailable: true),
        ]);

        result.SemanticAvailable.Should().BeTrue();
    }

    [Fact]
    public void Grounding_scores_are_excluded_from_retrieval_confidence()
    {
        // check_grounding scores an answer against passages; averaging that together with
        // top-match relevance would produce a number that measures nothing.
        var result = AiDashboardAggregation.Retrieval(
        [
            Invocation("find_similar_tickets", AssistantToolKind.DuplicateCheck, confidence: 0.6, matchCount: 1, semanticAvailable: true),
            Invocation("check_grounding", AssistantToolKind.Evaluation, confidence: 0.2),
        ]);

        result.ConfidenceSamples.Should().Be(1);
        result.AverageConfidence.Should().Be(0.6);
    }

    [Fact]
    public void Average_confidence_is_null_when_nothing_reported_a_score()
    {
        var result = AiDashboardAggregation.Retrieval(
        [
            Invocation("search_tickets", AssistantToolKind.Retrieval, matchCount: 4),
        ]);

        result.AverageConfidence.Should().BeNull();
        result.ConfidenceSamples.Should().Be(0);
    }

    [Fact]
    public void Tools_are_grouped_with_errors_and_confidence_per_tool()
    {
        var tools = AiDashboardAggregation.Tools(
        [
            Invocation("create_ticket", AssistantToolKind.Action),
            Invocation("create_ticket", AssistantToolKind.Action, isError: true),
            Invocation("search_knowledge_base", AssistantToolKind.Retrieval, confidence: 0.4, matchCount: 1, semanticAvailable: true),
            Invocation("search_knowledge_base", AssistantToolKind.Retrieval, confidence: 0.6, matchCount: 1, semanticAvailable: true),
            Invocation("search_knowledge_base", AssistantToolKind.Retrieval, confidence: 0.8, matchCount: 1, semanticAvailable: true),
        ]);

        tools.Should().HaveCount(2);

        var knowledge = tools.Single(t => t.ToolName == "search_knowledge_base");
        knowledge.Invocations.Should().Be(3);
        knowledge.Errors.Should().Be(0);
        knowledge.ErrorRate.Should().Be(0);
        knowledge.AverageConfidence.Should().BeApproximately(0.6, 1e-9);

        var create = tools.Single(t => t.ToolName == "create_ticket");
        create.Errors.Should().Be(1);
        create.ErrorRate.Should().Be(0.5);
        create.AverageConfidence.Should().BeNull("create_ticket reports no confidence");
    }

    [Fact]
    public void Average_latency_is_reported_per_tool()
    {
        var tools = AiDashboardAggregation.Tools(
        [
            Invocation("search_tickets", AssistantToolKind.Retrieval, durationMs: 100),
            Invocation("search_tickets", AssistantToolKind.Retrieval, durationMs: 300),
            Invocation("create_ticket", AssistantToolKind.Action, durationMs: 50),
        ]);

        tools.Single(t => t.ToolName == "search_tickets").AverageLatencyMs.Should().Be(200);
        tools.Single(t => t.ToolName == "create_ticket").AverageLatencyMs.Should().Be(50);
    }

    [Fact]
    public void A_failed_call_still_contributes_its_latency()
    {
        // A tool that fails slowly is the interesting case; excluding errors would hide it.
        var tools = AiDashboardAggregation.Tools(
        [
            Invocation("search_knowledge_base", AssistantToolKind.Retrieval, durationMs: 20),
            Invocation("search_knowledge_base", AssistantToolKind.Retrieval, isError: true, durationMs: 980),
        ]);

        tools.Single().AverageLatencyMs.Should().Be(500);
    }

    [Fact]
    public void Tools_are_ordered_by_call_count_then_by_name()
    {
        var tools = AiDashboardAggregation.Tools(
        [
            Invocation("zeta", AssistantToolKind.Action),
            Invocation("alpha", AssistantToolKind.Action),
            Invocation("busy", AssistantToolKind.Action),
            Invocation("busy", AssistantToolKind.Action),
        ]);

        tools.Select(t => t.ToolName).Should().Equal("busy", "alpha", "zeta");
    }

    [Fact]
    public void Empty_window_yields_no_tools_and_no_rates()
    {
        AiDashboardAggregation.Tools([]).Should().BeEmpty();

        var retrieval = AiDashboardAggregation.Retrieval([]);
        retrieval.DuplicateRate.Should().BeNull();
        retrieval.AverageConfidence.Should().BeNull();
        retrieval.SemanticAvailable.Should().BeFalse();
    }

    [Fact]
    public void Automation_rate_is_null_without_actions_and_a_share_otherwise()
    {
        new AutomationDto(AiActions: 0, TotalActions: 0).Rate.Should().BeNull();
        new AutomationDto(AiActions: 3, TotalActions: 12).Rate.Should().Be(0.25);
    }
}
