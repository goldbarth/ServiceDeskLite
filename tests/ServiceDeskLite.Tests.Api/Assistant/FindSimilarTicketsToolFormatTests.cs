using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class FindSimilarTicketsToolFormatTests
{
    private static HybridTicketMatch Match(
        double relevance, double? similarity, bool fromSemantic, bool fromKeyword, string title = "Printer offline") =>
        new(new TicketId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            title, TicketStatus.New, TicketPriority.High, relevance, similarity, fromSemantic, fromKeyword);

    [Fact]
    public void TopRelevance_ReturnsMax()
    {
        var result = new HybridTicketSearchResult(true,
        [
            Match(0.42, 0.4, true, false),
            Match(0.91, 0.9, true, true),
            Match(0.70, null, false, true),
        ]);

        FindSimilarTicketsTool.TopRelevance(result).Should().Be(0.91);
    }

    [Fact]
    public void TopRelevance_Empty_IsZero()
    {
        FindSimilarTicketsTool.TopRelevance(new HybridTicketSearchResult(true, [])).Should().Be(0.0);
    }

    [Fact]
    public void FormatResult_WithMatches_ShowsRelevanceSignalsAndSimilarity()
    {
        var result = new HybridTicketSearchResult(true, [Match(0.87, 0.83, true, true)]);

        var text = FindSimilarTicketsTool.FormatResult(result, "printer broken");

        text.Should().Contain("hybrid semantic + keyword search");
        text.Should().Contain("11111111-2222-3333-4444-555555555555");
        text.Should().Contain("\"Printer offline\"");
        text.Should().Contain("status=New");
        text.Should().Contain("priority=High");
        text.Should().Contain("relevance=87");
        text.Should().Contain("similarity=83");
        text.Should().Contain("matched=semantic+keyword");
    }

    [Fact]
    public void FormatResult_KeywordOnlyHit_OmitsSimilarityAndLabelsSignal()
    {
        var result = new HybridTicketSearchResult(true, [Match(0.5, null, false, true)]);

        var text = FindSimilarTicketsTool.FormatResult(result, "q");

        text.Should().Contain("matched=keyword");
        text.Should().NotContain("similarity=");
    }

    [Fact]
    public void FormatResult_SemanticUnavailable_LabelsAsWeakerEvidence()
    {
        var result = new HybridTicketSearchResult(false, [Match(1.0, null, false, true)]);

        var text = FindSimilarTicketsTool.FormatResult(result, "q");

        text.Should().Contain("keyword-only search");
        text.Should().Contain("weaker evidence");
    }

    [Fact]
    public void FormatResult_NoMatches_SemanticAvailable_SaysNoneSimilar()
    {
        var text = FindSimilarTicketsTool.FormatResult(new HybridTicketSearchResult(true, []), "anything");

        text.Should().Contain("No tickets similar");
    }

    [Fact]
    public void FormatResult_NoMatches_SemanticUnavailable_SaysNoKeywordMatch()
    {
        var text = FindSimilarTicketsTool.FormatResult(new HybridTicketSearchResult(false, []), "anything");

        text.Should().Contain("Semantic search is unavailable");
        text.Should().Contain("by keyword either");
    }
}
