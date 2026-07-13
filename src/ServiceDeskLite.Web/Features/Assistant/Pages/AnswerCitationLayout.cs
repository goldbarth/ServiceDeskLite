using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Web.Features.Assistant.Pages;

/// <summary>
/// Splits a streamed assistant answer into literal text and inline citation badges.
/// The model cites knowledge-base passages by title (per the system prompt, usually
/// quoted), so a title match anchors a badge to the exact claim it backs. Citations
/// whose title never appears in the answer stay <see cref="UnanchoredCitations"/> and
/// fall back to the sources list, so no evidence is dropped.
/// </summary>
public sealed record AnswerCitationLayout(
    IReadOnlyList<AnswerRun> Runs,
    IReadOnlyList<NumberedCitation> UnanchoredCitations)
{
    public static AnswerCitationLayout Build(string text, IReadOnlyList<AssistantCitation> citations)
    {
        var numbered = citations
            .Select((citation, index) => new NumberedCitation(index + 1, citation))
            .ToList();

        if (string.IsNullOrEmpty(text) || numbered.Count == 0)
            return new AnswerCitationLayout([new AnswerTextRun(text)], numbered);

        var anchors = new List<(int End, NumberedCitation Citation)>();
        var anchoredNumbers = new HashSet<int>();

        foreach (var candidate in numbered)
        {
            var title = candidate.Citation.Title;
            if (string.IsNullOrWhiteSpace(title))
                continue;

            var start = text.IndexOf(title, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                continue;

            var end = start + title.Length;
            // Place the badge after a trailing closing quote so it reads as `"Title" [n]`.
            if (end < text.Length && IsClosingQuote(text[end]))
                end++;

            anchors.Add((end, candidate));
            anchoredNumbers.Add(candidate.Number);
        }

        if (anchors.Count == 0)
            return new AnswerCitationLayout([new AnswerTextRun(text)], numbered);

        anchors.Sort((left, right) => left.End.CompareTo(right.End));

        var runs = new List<AnswerRun>();
        var cursor = 0;
        foreach (var (end, citation) in anchors)
        {
            if (end > cursor)
                runs.Add(new AnswerTextRun(text[cursor..end]));

            runs.Add(new AnswerBadgeRun(citation));
            cursor = Math.Max(cursor, end);
        }

        if (cursor < text.Length)
            runs.Add(new AnswerTextRun(text[cursor..]));

        var unanchored = numbered.Where(candidate => !anchoredNumbers.Contains(candidate.Number)).ToList();
        return new AnswerCitationLayout(runs, unanchored);
    }

    private static bool IsClosingQuote(char c) => c is '"' or '”' or '\'' or '’' or '»';
}

/// <summary>A knowledge-base citation paired with its 1-based display number.</summary>
public sealed record NumberedCitation(int Number, AssistantCitation Citation);

public abstract record AnswerRun;

public sealed record AnswerTextRun(string Text) : AnswerRun;

public sealed record AnswerBadgeRun(NumberedCitation Citation) : AnswerRun;
