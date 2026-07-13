using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Infrastructure.Embeddings;

namespace ServiceDeskLite.Tests.Api.Assistant;

/// <summary>
/// The semantic grounding evaluator (ADR-0040, issue #188), driven by a fake embedder so the
/// meaning-based judgement is deterministic. These cover exactly the cases the lexical evaluator
/// got wrong - a faithful paraphrase and a cross-lingual answer share no words with the source yet
/// must count as grounded - and the fallback that keeps a verdict coming when embeddings are gone.
/// </summary>
public sealed class SemanticGroundingEvaluatorTests
{
    private const string Passage =
        "To reset a locked account, open the admin console and choose Reset Password.";

    // Vectors are assigned by meaning, not by words: the passage and its faithful restatements
    // point the same way; the hallucination points elsewhere. Cosine then does what embeddings do.
    private static readonly float[] ResetConcept = [1f, 0f, 0f];
    private static readonly float[] OtherConcept = [0f, 1f, 0f];

    private const string Paraphrase =
        "Ask an administrator to trigger a credential renewal from the management panel.";
    private const string German =
        "Öffne die Admin-Konsole und wähle Passwort zurücksetzen.";
    private const string Hallucination =
        "Phone the night porter, who keeps the master key in a drawer beneath the fire alarm.";

    private static SemanticGroundingEvaluator Evaluator(IEmbeddingClient? embedder, bool configured = true) =>
        new(
            embedder,
            new VoyageOptions { ApiKey = configured ? "real-key" : VoyageOptions.DisabledPlaceholder },
            new LexicalGroundingEvaluator(),
            NullLogger<SemanticGroundingEvaluator>.Instance);

    private static FakeEmbedder MeaningEmbedder() => new(text => text switch
    {
        Passage or Paraphrase or German => ResetConcept,
        _ => OtherConcept,
    });

    [Fact]
    public async Task A_faithful_paraphrase_sharing_no_words_is_grounded()
    {
        var report = await Evaluator(MeaningEmbedder()).EvaluateAsync(Paraphrase, [Passage], default);

        report.Verdict.Should().Be(GroundingVerdict.Grounded,
            "the paraphrase restates the passage's meaning, which is what grounding is about");
        report.Unsupported.Should().BeEmpty();
    }

    [Fact]
    public async Task A_correct_answer_in_another_language_is_grounded()
    {
        var report = await Evaluator(MeaningEmbedder()).EvaluateAsync(German, [Passage], default);

        report.Verdict.Should().Be(GroundingVerdict.Grounded,
            "a German answer against an English passage shares no words but the same meaning");
    }

    [Fact]
    public async Task An_unsupported_claim_is_ungrounded_and_listed()
    {
        var report = await Evaluator(MeaningEmbedder()).EvaluateAsync(Hallucination, [Passage], default);

        report.Verdict.Should().Be(GroundingVerdict.Ungrounded);
        report.Unsupported.Should().ContainSingle().Which.Should().Contain("night porter");
    }

    [Fact]
    public async Task Without_an_embedder_it_falls_back_to_the_lexical_evaluator()
    {
        // No embedder at all (the InMemory case): the verbatim restatement is still gradable,
        // and it grades exactly as the lexical evaluator would.
        var report = await Evaluator(embedder: null).EvaluateAsync(Passage, [Passage], default);

        report.Should().BeEquivalentTo(GroundingEvaluator.Evaluate(Passage, [Passage]));
    }

    [Fact]
    public async Task When_voyage_is_not_configured_it_falls_back_to_the_lexical_evaluator()
    {
        // An embedder is present but the key is a placeholder; the doomed call is never made.
        var embedder = new FakeEmbedder(_ => throw new InvalidOperationException("must not be called"));

        var report = await Evaluator(embedder, configured: false).EvaluateAsync(Passage, [Passage], default);

        report.Should().BeEquivalentTo(GroundingEvaluator.Evaluate(Passage, [Passage]));
    }

    [Fact]
    public async Task An_embedding_failure_falls_back_to_the_lexical_evaluator()
    {
        var embedder = new FakeEmbedder(_ => throw new HttpRequestException("Voyage down"));

        var report = await Evaluator(embedder).EvaluateAsync(Passage, [Passage], default);

        report.Should().BeEquivalentTo(GroundingEvaluator.Evaluate(Passage, [Passage]),
            "a transient embedding failure must not leave the check with no verdict");
    }

    private sealed class FakeEmbedder(Func<string, float[]> vectorFor) : IEmbeddingClient
    {
        public Task<IReadOnlyList<float[]>> EmbedAsync(
            IReadOnlyList<string> inputs, EmbeddingInputType inputType, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(vectorFor).ToList());
    }
}
