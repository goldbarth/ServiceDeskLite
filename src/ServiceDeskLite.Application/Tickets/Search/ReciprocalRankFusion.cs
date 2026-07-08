namespace ServiceDeskLite.Application.Tickets.Search;

/// <summary>
/// Reciprocal Rank Fusion (RRF): merges several ranked lists into one score per item
/// using only rank position, not each signal's raw score. This is the key property —
/// cosine similarity and keyword recency live on incompatible scales, so fusing by
/// rank avoids brittle score normalization between them. Each signal contributes
/// <c>weight / (k + rank)</c>; the constant <c>k</c> (default 60, the value from the
/// original RRF paper) damps the influence of low ranks. Pure and side-effect free
/// so the ranking function is unit-tested in isolation.
/// </summary>
public static class ReciprocalRankFusion
{
    public const int DefaultK = 60;

    /// <summary>
    /// Fuses ranked lists (each item list ordered best-first) into item → summed score.
    /// An item missing from a list simply contributes nothing for that signal.
    /// </summary>
    public static IReadOnlyDictionary<T, double> Fuse<T>(
        IReadOnlyList<(IReadOnlyList<T> Ranking, double Weight)> signals,
        int k = DefaultK)
        where T : notnull
    {
        var scores = new Dictionary<T, double>();

        foreach (var (ranking, weight) in signals)
        {
            for (var i = 0; i < ranking.Count; i++)
            {
                // 1-based rank so the top item is rank 1, contributing weight/(k+1).
                var contribution = weight / (k + i + 1);
                scores[ranking[i]] = scores.GetValueOrDefault(ranking[i]) + contribution;
            }
        }

        return scores;
    }

    /// <summary>Scales scores so the strongest is 1.0, turning raw RRF sums into a relative confidence.</summary>
    public static double Normalize(double score, double maxScore) =>
        maxScore > 0 ? score / maxScore : 0.0;
}
