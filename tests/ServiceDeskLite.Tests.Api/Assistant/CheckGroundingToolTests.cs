using System.Text.Json;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Application.Abstractions.Search;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class CheckGroundingToolTests
{
    private static JsonElement Input(string answer) =>
        JsonSerializer.Deserialize<JsonElement>($$"""{ "answer": {{JsonSerializer.Serialize(answer)}} }""");

    private static IRagRetrievalContext ContextWith(params string[] contents)
    {
        var ctx = new RagRetrievalContext();
        ctx.Record(contents.Select(c => new RagPassage("Title", "Heading", c)));
        return ctx;
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "answer": "" }""")]
    [InlineData("""{ "answer": 3 }""")]
    public void TryParseInput_Invalid_Fails(string json)
    {
        var ok = CheckGroundingTool.TryParseInput(JsonSerializer.Deserialize<JsonElement>(json), out _, out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public async Task NoPassagesRetrieved_ReportsNothingToGround()
    {
        var tool = new CheckGroundingTool(new RagRetrievalContext());

        var result = await tool.ExecuteAsync(Input("some answer"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Content.Should().Contain("nothing to ground");
    }

    [Fact]
    public async Task GroundedDraft_ReportsHighScoreAndClearsToAnswer()
    {
        var tool = new CheckGroundingTool(ContextWith(
            "Reset your password at the self-service portal after verifying your identity with the second factor."));

        var result = await tool.ExecuteAsync(
            Input("Reset your password at the self-service portal using your second factor."),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Confidence.Should().BeGreaterThanOrEqualTo(GroundingEvaluator.GroundedThreshold);
        result.Content.Should().Contain("Grounded");
        result.Content.Should().Contain("well supported");
    }

    [Fact]
    public async Task UngroundedDraft_ReportsLowScoreAndInstructsCorrection()
    {
        var tool = new CheckGroundingTool(ContextWith(
            "Reset your password at the self-service portal after verifying your identity."));

        var result = await tool.ExecuteAsync(
            Input("Call the executive hotline and recite your grandmother passphrase immediately."),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Confidence.Should().BeLessThan(GroundingEvaluator.WeakThreshold);
        result.Content.Should().Contain("Unsupported statements");
        result.Content.Should().Contain("hedge");
    }

    [Fact]
    public async Task SearchThenCheck_GroundsAgainstRetrievedPassages()
    {
        // The record → check wiring: search_knowledge_base stores its passages in the
        // shared context, and check_grounding scores the draft against exactly those.
        var context = new RagRetrievalContext();

        var matches = new[]
        {
            new KnowledgeMatch("pw", "Password Reset", "Article", "Self-Service Reset",
                "Reset your password at the self-service portal after verifying your second factor.", 0.9),
        };
        var searchTool = new SearchKnowledgeBaseTool(
            new StubKnowledgeSearch(new KnowledgeSearchResult(true, matches)),
            context,
            NullLogger<SearchKnowledgeBaseTool>.Instance);

        await searchTool.ExecuteAsync(
            JsonSerializer.Deserialize<JsonElement>("""{ "query": "reset password" }"""), CancellationToken.None);

        context.Passages.Should().ContainSingle();

        var groundingTool = new CheckGroundingTool(context);
        var result = await groundingTool.ExecuteAsync(
            Input("Reset your password at the self-service portal using your second factor."),
            CancellationToken.None);

        result.Confidence.Should().BeGreaterThanOrEqualTo(GroundingEvaluator.GroundedThreshold);
    }

    private sealed class StubKnowledgeSearch(KnowledgeSearchResult result) : IKnowledgeBaseSearch
    {
        public Task<KnowledgeSearchResult> SearchAsync(string query, int limit, CancellationToken ct) =>
            Task.FromResult(result);
    }
}
