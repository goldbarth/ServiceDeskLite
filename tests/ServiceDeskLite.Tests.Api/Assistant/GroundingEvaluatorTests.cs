using FluentAssertions;

using ServiceDeskLite.Api.Assistant;

namespace ServiceDeskLite.Tests.Api.Assistant;

/// <summary>
/// Deterministic RAG grounding evaluation (issue #158) against fixed fixtures — no
/// model or embedding call. Proves a grounded answer scores high, a hallucinated one
/// scores low, and a partly-supported one lands in the "weak" band with the unsupported
/// sentence flagged.
/// </summary>
public sealed class GroundingEvaluatorTests
{
    private static readonly string[] Sources =
    [
        "Reset your password at the self-service portal after verifying your identity with the second factor.",
        "An account locks after five failed sign-ins and unlocks automatically after fifteen minutes.",
    ];

    [Fact]
    public void GroundedAnswer_ScoresHighAndIsGrounded()
    {
        var report = GroundingEvaluator.Evaluate(
            "Reset your password at the self-service portal using your second factor.", Sources);

        report.Verdict.Should().Be(GroundingVerdict.Grounded);
        report.Score.Should().BeGreaterThanOrEqualTo(GroundingEvaluator.GroundedThreshold);
        report.Unsupported.Should().BeEmpty();
    }

    [Fact]
    public void HallucinatedAnswer_ScoresLowAndIsUngrounded()
    {
        var report = GroundingEvaluator.Evaluate(
            "Call the executive hotline and recite your grandmother passphrase immediately.", Sources);

        report.Verdict.Should().Be(GroundingVerdict.Ungrounded);
        report.Score.Should().BeLessThan(GroundingEvaluator.WeakThreshold);
        report.Unsupported.Should().NotBeEmpty();
    }

    [Fact]
    public void PartlyGroundedAnswer_IsWeakAndFlagsUnsupportedSentence()
    {
        var report = GroundingEvaluator.Evaluate(
            "Reset your password at the portal. Then email the executive your passphrase for verification.",
            Sources);

        report.Verdict.Should().Be(GroundingVerdict.Weak);
        report.Score.Should().Be(0.5);
        report.Unsupported.Should().ContainSingle()
            .Which.Should().Contain("passphrase");
    }

    [Fact]
    public void AnswerWithNoSubstantiveClaims_IsVacuouslyGrounded()
    {
        var report = GroundingEvaluator.Evaluate("...", Sources);

        report.Score.Should().Be(1.0);
        report.Verdict.Should().Be(GroundingVerdict.Grounded);
        report.Unsupported.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0.6, GroundingVerdict.Grounded)]
    [InlineData(0.9, GroundingVerdict.Grounded)]
    [InlineData(0.5, GroundingVerdict.Weak)]
    [InlineData(0.3, GroundingVerdict.Weak)]
    [InlineData(0.29, GroundingVerdict.Ungrounded)]
    [InlineData(0.0, GroundingVerdict.Ungrounded)]
    public void VerdictFor_BandsByThreshold(double score, GroundingVerdict expected)
    {
        GroundingEvaluator.VerdictFor(score).Should().Be(expected);
    }
}
