using Anthropic;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Search;

namespace ServiceDeskLite.Tests.Evaluation.Harness;

/// <summary>
/// The real API, with one thing replaced: the transport under the Anthropic client.
/// </summary>
/// <remarks>
/// Everything else runs for real — the endpoint, the SSE serialization, the agentic loop, the
/// guard pipeline, the tools, the command handlers, the audit trail. That is the point: the suite
/// evaluates the agent as deployed, and the only fiction is what the model decided to say.
/// </remarks>
public sealed class EvaluationHost : WebApplicationFactory<Program>
{
    private const string TestApiKey = "test-api-key";

    public ScriptedModelHandler Model { get; } = new();

    /// <summary>Passages the knowledge base returns, when a scenario needs retrieval to succeed.</summary>
    public ScriptedKnowledgeBase KnowledgeBase { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        // UseSetting, not ConfigureAppConfiguration: the composition root reads
        // Persistence:Provider while the service collection is being built, which happens
        // before the factory's configuration callbacks run.
        builder.UseSetting("Persistence:Provider", "InMemory");
        builder.UseSetting("Auth:ApiKey", TestApiKey);
        builder.UseSetting("Anthropic:ApiKey", "test-anthropic-key");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(new AnthropicClient
            {
                ApiKey = "test-anthropic-key",
                HttpClient = new HttpClient(Model),
            });

            services.AddSingleton<IKnowledgeBaseSearch>(KnowledgeBase);
        });
    }

    protected override void ConfigureClient(HttpClient client)
        => client.DefaultRequestHeaders.Add("X-Api-Key", TestApiKey);
}
