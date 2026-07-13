namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Scores how well a drafted answer is supported by the passages it was drawn from, so
/// <c>check_grounding</c> can steer the model to hedge or re-retrieve on weak support (ADR-0031,
/// ADR-0040). An async port because the default implementation embeds the text; the lexical
/// evaluator behind it stays synchronous and is the fallback when embeddings are unavailable.
/// </summary>
public interface IGroundingEvaluator
{
    Task<GroundingReport> EvaluateAsync(
        string answer, IReadOnlyList<string> sources, CancellationToken ct);
}

/// <summary>
/// Word-overlap grounding as a port implementation. Deterministic and free, it is both the
/// InMemory default and the fallback the semantic evaluator drops to when it cannot embed. Its
/// known blind spot (a correct paraphrase or a cross-lingual answer scores low, issue #188) is why
/// it is no longer the primary path where embeddings exist.
/// </summary>
public sealed class LexicalGroundingEvaluator : IGroundingEvaluator
{
    public Task<GroundingReport> EvaluateAsync(
        string answer, IReadOnlyList<string> sources, CancellationToken ct) =>
        Task.FromResult(GroundingEvaluator.Evaluate(answer, sources));
}
