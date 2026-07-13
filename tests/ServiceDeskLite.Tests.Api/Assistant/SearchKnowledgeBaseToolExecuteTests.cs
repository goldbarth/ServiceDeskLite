using System.Text.Json;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Application.Abstractions.Search;

namespace ServiceDeskLite.Tests.Api.Assistant;

/// <summary>
/// Behaviour of the search_knowledge_base tool across the three retrieval outcomes.
/// The honest-degradation contract (issue #156): when search is unavailable or empty
/// the tool must return no citations and tell the model not to invent a source.
/// </summary>
public sealed class SearchKnowledgeBaseToolExecuteTests
{
    private sealed class StubSearch(KnowledgeSearchResult result) : IKnowledgeBaseSearch
    {
        public Task<KnowledgeSearchResult> SearchAsync(string query, int limit, CancellationToken ct) =>
            Task.FromResult(result);
    }

    private sealed class ThrowingSearch(Exception fault) : IKnowledgeBaseSearch
    {
        public Task<KnowledgeSearchResult> SearchAsync(string query, int limit, CancellationToken ct) =>
            throw fault;
    }

    private static SearchKnowledgeBaseTool ThrowingTool(Exception fault) =>
        new(new ThrowingSearch(fault), new RagRetrievalContext(), NullLogger<SearchKnowledgeBaseTool>.Instance);

    private static JsonElement Input(string query) =>
        JsonSerializer.Deserialize<JsonElement>($$"""{ "query": {{JsonSerializer.Serialize(query)}} }""");

    private static SearchKnowledgeBaseTool Tool(KnowledgeSearchResult result) =>
        new(new StubSearch(result), new RagRetrievalContext(), NullLogger<SearchKnowledgeBaseTool>.Instance);

    [Fact]
    public async Task Unavailable_reports_honestly_without_citations()
    {
        var tool = Tool(KnowledgeSearchResult.Unavailable);

        var result = await tool.ExecuteAsync(Input("how to reset password"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Citations.Should().BeNull();
        result.Content.Should().Contain("not available");
    }

    [Fact]
    public async Task No_matches_returns_no_citations()
    {
        var tool = Tool(new KnowledgeSearchResult(IsAvailable: true, Matches: []));

        var result = await tool.ExecuteAsync(Input("something obscure"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Citations.Should().BeNull();
        result.Content.Should().Contain("No knowledge-base passages matched");
    }

    [Fact]
    public async Task A_technical_failure_is_an_error_distinct_from_unavailable()
    {
        // A broken search (Postgres/Voyage down) is not honest degradation: it must read as an
        // error, so the dashboard and the retry policy see a failure - not a clean invocation.
        var tool = ThrowingTool(new InvalidOperationException("knowledge base query failed"));

        var result = await tool.ExecuteAsync(Input("how to reset password"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Content.Should().Contain("technical error");

        // The two conditions must not collapse into the same tool result.
        var unavailable = await Tool(KnowledgeSearchResult.Unavailable)
            .ExecuteAsync(Input("how to reset password"), CancellationToken.None);
        unavailable.IsError.Should().BeFalse();
        result.Content.Should().NotBe(unavailable.Content);
    }

    [Fact]
    public async Task A_transient_failure_is_rethrown_for_the_retry_policy()
    {
        // Transient faults belong to ToolRetryPolicy, not to the catch that reports an error.
        var tool = ThrowingTool(new HttpRequestException("upstream", null, System.Net.HttpStatusCode.ServiceUnavailable));

        var act = () => tool.ExecuteAsync(Input("how to reset password"), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Matches_return_citations_and_top_confidence()
    {
        var matches = new[]
        {
            new KnowledgeMatch("pw", "Password Reset and Account Lockout", "Article", "Self-Service Reset",
                "Users reset their own password at the portal.", 0.91),
            new KnowledgeMatch("pw", "Password Reset and Account Lockout", "Article", "Lockout Policy",
                "An account locks after five failed sign-ins.", 0.74),
        };
        var tool = Tool(new KnowledgeSearchResult(IsAvailable: true, matches));

        var result = await tool.ExecuteAsync(Input("reset my password"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Confidence.Should().Be(0.91);
        result.Citations.Should().NotBeNull();
        result.Citations!.Should().HaveCount(2);
        result.Citations![0].Title.Should().Be("Password Reset and Account Lockout");
    }
}
