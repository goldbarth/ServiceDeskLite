using Anthropic;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Api.Assistant.Sandbox;
using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Api.Composition;

public static class AssistantComposition
{
    public static IServiceCollection AddAssistant(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AnthropicOptions>()
            .Bind(configuration.GetSection(AnthropicOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey),
                "Anthropic:ApiKey is not configured. Set it via user-secrets: " +
                "dotnet user-secrets set Anthropic:ApiKey <key>")
            .Validate(o => o.MaxToolIterations is > 0 and <= 10,
                "Anthropic:MaxToolIterations must be between 1 and 10.")
            .Validate(o => o.SummaryMaxTokens is > 0 and <= 8192,
                "Anthropic:SummaryMaxTokens must be between 1 and 8192.")
            .Validate(o => o.MaxToolRetries is >= 0 and <= 5,
                "Anthropic:MaxToolRetries must be between 0 and 5.")
            .Validate(o => o.ToolRetryBaseDelayMs is >= 0 and <= 5000,
                "Anthropic:ToolRetryBaseDelayMs must be between 0 and 5000.")
            .Validate(o => IsValidTimeZone(o.UserTimeZone),
                "Anthropic:UserTimeZone must be a valid timezone id (e.g. Europe/Berlin).")
            .ValidateOnStart();

        services.AddSingleton(sp => new AnthropicClient
        {
            ApiKey = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value.ApiKey,
        });

        services.AddAgentSandbox(configuration);

        services.AddSingleton<ICurrentUser, DemoCurrentUser>();

        // Per-request: search_knowledge_base records retrieved passages here, check_grounding reads them.
        services.AddScoped<IRagRetrievalContext, RagRetrievalContext>();

        services.AddScoped<CreateTicketTool>();
        services.AddScoped<UpdateTicketTool>();
        services.AddScoped<FindSimilarTicketsTool>();
        services.AddScoped<SearchTicketsTool>();
        services.AddScoped<ChangeTicketStatusTool>();
        services.AddScoped<AssignTicketTool>();
        services.AddScoped<RouteTicketTool>();
        services.AddScoped<SearchKnowledgeBaseTool>();
        services.AddScoped<CheckGroundingTool>();
        services.AddScoped<RememberTool>();
        services.AddScoped<RecallMemoryTool>();
        services.AddScoped<AssistantChatService>();
        services.AddScoped<TicketSummaryService>();

        return services;
    }

    /// <summary>
    /// The guard layer every tool call passes through (ADR-0035). Guards are singletons: they
    /// hold no per-request state, and the per-turn counters are passed to them in the context.
    /// </summary>
    private static IServiceCollection AddAgentSandbox(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AgentSandboxOptions>()
            .Bind(configuration.GetSection(AgentSandboxOptions.SectionName))
            .Validate(o => o.MaxInputCharacters is > 0 and <= 200_000,
                "AgentSandbox:MaxInputCharacters must be between 1 and 200000.")
            .Validate(o => o.MaxStringCharacters is > 0 and <= 100_000,
                "AgentSandbox:MaxStringCharacters must be between 1 and 100000.")
            .Validate(o => o.MaxStringCharacters <= o.MaxInputCharacters,
                "AgentSandbox:MaxStringCharacters must not exceed MaxInputCharacters, "
                + "or the string limit could never be reached.")
            .Validate(o => o.MaxWritesPerTurn is > 0 and <= 50,
                "AgentSandbox:MaxWritesPerTurn must be between 1 and 50.")
            .Validate(o => o.ToolCallsPerMinute is > 0 and <= 10_000,
                "AgentSandbox:ToolCallsPerMinute must be between 1 and 10000.")
            .Validate(o => o.ModelTurnsPerMinute is > 0 and <= 10_000,
                "AgentSandbox:ModelTurnsPerMinute must be between 1 and 10000.")
            .ValidateOnStart();

        services.AddSingleton<TokenBucketRegistry>();
        services.AddSingleton<ModelTurnLimiter>();

        services.AddSingleton<IToolGuard, KnownToolGuard>();
        services.AddSingleton<IToolGuard, InputSizeGuard>();
        services.AddSingleton<IToolGuard, WriteBudgetGuard>();
        services.AddSingleton<IToolGuard, RateLimitGuard>();
        services.AddSingleton<ToolGuardPipeline>();

        return services;
    }

    private static bool IsValidTimeZone(string id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}
