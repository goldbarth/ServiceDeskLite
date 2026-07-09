using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Tests.Api.Assistant;

/// <summary>
/// The parser is the load-bearing part of streamed summaries: markers arrive split across
/// arbitrary token boundaries, so a naive per-chunk match either leaks marker fragments
/// into the UI or drops the section switch entirely.
/// </summary>
public sealed class SummarySectionParserTests
{
    /// <summary>Feeds every chunk and returns the deltas of the whole stream, including the flushed tail.</summary>
    private static List<SummaryDelta> Run(params string[] chunks)
    {
        var parser = new SummarySectionParser();

        List<SummaryDelta> deltas = [];
        foreach (var chunk in chunks)
            deltas.AddRange(parser.Feed(chunk));

        deltas.AddRange(parser.Flush());
        return deltas;
    }

    private static string TextOf(IEnumerable<SummaryDelta> deltas, TicketSummarySection section) =>
        string.Concat(deltas.Where(d => d.Section == section).Select(d => d.Text));

    [Fact]
    public void Assigns_text_to_the_section_of_the_preceding_marker()
    {
        var deltas = Run("<<SUMMARY>>\nA login fails.\n<<NEXT_STEPS>>\n- Check logs\n");

        TextOf(deltas, TicketSummarySection.Summary).Should().Be("A login fails.\n");
        TextOf(deltas, TicketSummarySection.NextSteps).Should().Be("- Check logs\n");
    }

    [Fact]
    public void Recognizes_a_marker_split_across_chunks()
    {
        var deltas = Run("<<SUMMARY>>\nDone.\n<<NEXT", "_STE", "PS>>\n- Act");

        TextOf(deltas, TicketSummarySection.Summary).Should().Be("Done.\n");
        TextOf(deltas, TicketSummarySection.NextSteps).Should().Be("- Act");
    }

    [Fact]
    public void Recognizes_a_marker_split_after_its_first_character()
    {
        var deltas = Run("<", "<SUMMARY>", ">", "\nHello");

        TextOf(deltas, TicketSummarySection.Summary).Should().Be("Hello");
    }

    [Fact]
    public void Never_leaks_a_marker_fragment_as_text()
    {
        var deltas = Run("<<RISKS>>\nNone.\n<<MISS", "ING_INFO>>\nNothing.");

        deltas.Should().NotContain(d => d.Text.Contains('<'));
        TextOf(deltas, TicketSummarySection.Risks).Should().Be("None.\n");
        TextOf(deltas, TicketSummarySection.MissingInfo).Should().Be("Nothing.");
    }

    [Fact]
    public void Emits_all_four_sections_in_order()
    {
        var deltas = Run(
            "<<SUMMARY>>\ns\n<<NEXT_STEPS>>\nn\n<<RISKS>>\nr\n<<MISSING_INFO>>\nm");

        deltas.Select(d => d.Section).Distinct().Should().Equal(
            TicketSummarySection.Summary,
            TicketSummarySection.NextSteps,
            TicketSummarySection.Risks,
            TicketSummarySection.MissingInfo);
    }

    [Fact]
    public void Drops_preamble_before_the_first_marker()
    {
        var deltas = Run("Here is the summary you asked for.\n<<SUMMARY>>\nReal content.");

        deltas.Should().OnlyContain(d => d.Section == TicketSummarySection.Summary);
        TextOf(deltas, TicketSummarySection.Summary).Should().Be("Real content.");
    }

    [Fact]
    public void Strips_the_newline_that_follows_a_marker()
    {
        var deltas = Run("<<SUMMARY>>", "\n", "Starts here.");

        TextOf(deltas, TicketSummarySection.Summary).Should().Be("Starts here.");
    }

    [Fact]
    public void Passes_through_an_angle_bracket_that_cannot_become_a_marker()
    {
        var deltas = Run("<<SUMMARY>>\nUse <b> tags and 3 < 5.");

        TextOf(deltas, TicketSummarySection.Summary).Should().Be("Use <b> tags and 3 < 5.");
    }

    [Fact]
    public void Flush_releases_a_dangling_marker_prefix_as_text()
    {
        var parser = new SummarySectionParser();
        parser.Feed("<<SUMMARY>>\nTruncated <<NEXT");

        var flushed = parser.Flush();

        TextOf(flushed, TicketSummarySection.Summary).Should().Be("<<NEXT");
    }

    [Fact]
    public void Holds_back_a_viable_marker_prefix_until_it_is_resolved()
    {
        var parser = new SummarySectionParser();
        parser.Feed("<<SUMMARY>>\nText.");

        parser.Feed("<<RIS").Should().BeEmpty("a viable marker prefix must not reach the client");
        parser.Feed("KS>>\nNone.").Should().ContainSingle()
            .Which.Section.Should().Be(TicketSummarySection.Risks);
    }

    [Fact]
    public void Character_by_character_streaming_yields_the_same_text_as_one_chunk()
    {
        const string output = "<<SUMMARY>>\ns\n<<NEXT_STEPS>>\n- a\n- b\n<<RISKS>>\nNone.\n<<MISSING_INFO>>\nNothing.";

        var single = Run(output);
        var split = Run([.. output.Select(c => c.ToString())]);

        foreach (var section in Enum.GetValues<TicketSummarySection>())
            TextOf(split, section).Should().Be(TextOf(single, section));
    }
}
