using System.Text.RegularExpressions;

namespace ServiceDeskLite.Api.Assistant;

public enum GroundingVerdict
{
    /// <summary>Well supported by the sources — safe to assert.</summary>
    Grounded,

    /// <summary>Partially supported — the model should hedge or re-retrieve before asserting.</summary>
    Weak,

    /// <summary>Largely unsupported — likely hallucinated; the model must not assert it as sourced fact.</summary>
    Ungrounded,
}

/// <summary>
/// Outcome of grounding an answer against its retrieved sources. <see cref="Score"/> is
/// the fraction of the answer's substantive sentences supported by the source text
/// (0..1); <see cref="Unsupported"/> lists the sentences that failed, so the model can
/// see exactly what to fix.
/// </summary>
public sealed record GroundingReport(
    double Score,
    GroundingVerdict Verdict,
    IReadOnlyList<string> Unsupported);

/// <summary>
/// Deterministic grounding / hallucination check (issue #158). Measures how much of an
/// answer is lexically supported by the passages it was supposed to be drawn from:
/// each substantive sentence is "supported" when enough of its content words appear in
/// the combined source vocabulary. Deterministic and side-effect free — the RAG
/// evaluation is exercised against fixtures in the test suite, independent of any model
/// or embedding call. It assumes answer and sources share a language (the check is
/// lexical); cross-lingual grounding is out of scope, see ADR-0031.
/// </summary>
public static class GroundingEvaluator
{
    // A sentence counts as supported when at least this fraction of its content words
    // appear in the sources; below it, the sentence likely introduces unsourced claims.
    private const double SentenceSupportThreshold = 0.5;

    // Verdict bands over the overall supported-sentence ratio.
    public const double GroundedThreshold = 0.6;
    public const double WeakThreshold = 0.3;

    private static readonly Regex WordPattern = new(@"[\p{L}\p{Nd}]{3,}", RegexOptions.Compiled);

    // Deliberately tiny: only words so common they carry no grounding signal. A larger
    // list risks discarding real content; RRF-style, keep the heuristic transparent.
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "are", "but", "not", "you", "your", "with", "this", "that",
        "from", "have", "has", "was", "were", "will", "would", "can", "could", "should",
        "into", "out", "via", "per", "any", "all", "may", "must", "when", "then", "than",
    };

    public static GroundingReport Evaluate(string answer, IReadOnlyList<string> sources)
    {
        var sourceVocab = ContentWords(string.Join('\n', sources));

        var evaluated = 0;
        var supported = 0;
        var unsupported = new List<string>();

        foreach (var sentence in SplitSentences(answer))
        {
            var words = ContentWords(sentence);
            if (words.Count == 0)
                continue; // nothing asserted (greeting, punctuation) — not a grounding claim

            evaluated++;
            var overlap = words.Count(w => sourceVocab.Contains(w)) / (double)words.Count;
            if (overlap >= SentenceSupportThreshold)
                supported++;
            else
                unsupported.Add(sentence.Trim());
        }

        // An answer that asserts nothing substantive (or has no sources to contradict it)
        // is vacuously grounded — there is nothing unsupported to flag.
        var score = evaluated == 0 ? 1.0 : supported / (double)evaluated;

        return new GroundingReport(score, VerdictFor(score), unsupported);
    }

    public static GroundingVerdict VerdictFor(double score) => score switch
    {
        >= GroundedThreshold => GroundingVerdict.Grounded,
        >= WeakThreshold => GroundingVerdict.Weak,
        _ => GroundingVerdict.Ungrounded,
    };

    private static HashSet<string> ContentWords(string text) =>
        WordPattern.Matches(text)
            .Select(m => m.Value.ToLowerInvariant())
            .Where(w => !StopWords.Contains(w))
            .ToHashSet();

    private static IEnumerable<string> SplitSentences(string text) =>
        Regex.Split(text ?? string.Empty, @"(?<=[.!?])\s+|\n+")
            .Where(s => !string.IsNullOrWhiteSpace(s));
}
