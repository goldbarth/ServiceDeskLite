using ServiceDeskLite.Infrastructure.Embeddings;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Grounding by meaning, not by shared words (ADR-0040, issue #188). Each substantive answer
/// sentence is embedded alongside the retrieved passages in one Voyage call, and a sentence counts
/// as supported when its closest passage is near enough in vector space. This is what fixes the
/// lexical evaluator's blind spot: a correct paraphrase or a cross-lingual answer lands close to
/// the source it restates, where word overlap saw nothing.
/// </summary>
/// <remarks>
/// Falls back to the lexical evaluator when it cannot embed - no Voyage key, or an embedding call
/// that fails - so the check still returns a verdict rather than nothing. In practice a grounding
/// check only runs after a knowledge-base retrieval, and retrieval already needs Voyage, so the
/// fallback is the transient-failure net, not the common path. The verdict bands over the
/// fraction of supported sentences are unchanged (<see cref="GroundingEvaluator.VerdictFor"/>);
/// only how a single sentence is judged supported moves from lexical to semantic.
/// </remarks>
public sealed class SemanticGroundingEvaluator : IGroundingEvaluator
{
    // Cosine similarity above which a sentence's nearest passage counts as supporting it.
    // Calibrated for voyage-3.5: a faithful paraphrase sits well above this, an unrelated
    // claim well below. A const, not configuration - it moves only if the model changes.
    public const double SemanticSupportThreshold = 0.6;

    private readonly IEmbeddingClient? _embeddings;
    private readonly VoyageOptions? _voyage;
    private readonly LexicalGroundingEvaluator _lexical;
    private readonly ILogger<SemanticGroundingEvaluator> _logger;

    public SemanticGroundingEvaluator(
        IEmbeddingClient? embeddings,
        VoyageOptions? voyage,
        LexicalGroundingEvaluator lexical,
        ILogger<SemanticGroundingEvaluator> logger)
    {
        _embeddings = embeddings;
        _voyage = voyage;
        _lexical = lexical ?? throw new ArgumentNullException(nameof(lexical));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GroundingReport> EvaluateAsync(
        string answer, IReadOnlyList<string> sources, CancellationToken ct)
    {
        if (_embeddings is null || _voyage?.IsConfigured != true || sources.Count == 0)
            return await _lexical.EvaluateAsync(answer, sources, ct);

        var sentences = GroundingEvaluator.SubstantiveSentences(answer);
        if (sentences.Count == 0)
            return new GroundingReport(1.0, GroundingVerdict.Grounded, []);

        IReadOnlyList<float[]> vectors;
        try
        {
            // One call: sentences first, passages after, so the split is positional.
            var inputs = sentences.Concat(sources).ToList();
            vectors = await _embeddings.EmbedAsync(inputs, EmbeddingInputType.Document, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Grounding embedding failed; falling back to lexical evaluation");
            return await _lexical.EvaluateAsync(answer, sources, ct);
        }

        var sentenceVectors = vectors.Take(sentences.Count).ToList();
        var sourceVectors = vectors.Skip(sentences.Count).ToList();

        var supported = 0;
        var unsupported = new List<string>();
        for (var i = 0; i < sentences.Count; i++)
        {
            var best = sourceVectors.Max(src => Cosine(sentenceVectors[i], src));
            if (best >= SemanticSupportThreshold)
                supported++;
            else
                unsupported.Add(sentences[i]);
        }

        var score = supported / (double)sentences.Count;
        return new GroundingReport(score, GroundingEvaluator.VerdictFor(score), unsupported);
    }

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var denom = Math.Sqrt(magA) * Math.Sqrt(magB);
        return denom == 0 ? 0 : dot / denom;
    }
}
