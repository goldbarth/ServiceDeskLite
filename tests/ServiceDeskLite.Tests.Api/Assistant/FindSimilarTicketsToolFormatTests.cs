using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class FindSimilarTicketsToolFormatTests
{
    private static TicketSimilarityMatch Match(double similarity) =>
        new(new TicketId(Guid.NewGuid()), "Printer offline", TicketStatus.New, TicketPriority.High, similarity);

    private static TicketListItemDto Item(string title) =>
        new(new TicketId(Guid.Parse("11111111-2222-3333-4444-555555555555")), title, TicketStatus.New,
            TicketPriority.High, DateTimeOffset.UtcNow, null, null, [], false, "#ABC123");

    [Fact]
    public void TopSimilarity_ReturnsMax()
    {
        FindSimilarTicketsTool.TopSimilarity([Match(0.42), Match(0.91), Match(0.7)]).Should().Be(0.91);
    }

    [Fact]
    public void TopSimilarity_Empty_IsZero()
    {
        FindSimilarTicketsTool.TopSimilarity([]).Should().Be(0.0);
    }

    [Fact]
    public void FormatFallback_WithHits_LabelsAsKeywordAndListsTickets()
    {
        var text = FindSimilarTicketsTool.FormatFallback(
            "printer broken", "Semantic search is unavailable here.", [Item("Printer offline")]);

        text.Should().Contain("Semantic search is unavailable here.");
        text.Should().Contain("Keyword-matched 1 ticket(s)");
        text.Should().Contain("less precise");
        text.Should().Contain("11111111-2222-3333-4444-555555555555");
        text.Should().Contain("\"Printer offline\"");
    }

    [Fact]
    public void FormatFallback_WithoutHits_SaysNoKeywordMatch()
    {
        var text = FindSimilarTicketsTool.FormatFallback("anything", "No semantic matches found.", []);

        text.Should().Contain("No semantic matches found.");
        text.Should().Contain("by keyword either");
    }
}
