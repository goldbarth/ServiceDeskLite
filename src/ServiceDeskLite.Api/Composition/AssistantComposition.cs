using Anthropic;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Api.Assistant.Agent;
using ServiceDeskLite.Api.Assistant.Sandbox;
using ServiceDeskLite.Api.Worker;
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

        // Interactive in every request scope; only the worker's own scope switches it (ADR-0037).
        // Registered by its concrete type as well, because the worker has to reach the setter.
        services.AddScoped<AgentActorContext>();
        services.AddScoped<IAgentActor>(sp => sp.GetRequiredService<AgentActorContext>());

        services.AddScoped<AddCommentTool>();
        services.AddScoped<ToolDispatcher>();
        services.AddScoped<AgentLoop>();

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

        // Only ever refuses an unattended agent; in a conversation the user is the review (ADR-0037).
        services.AddSingleton<IToolGuard, HumanReviewGuard>();

        services.AddSingleton<ToolGuardPipeline>();

        return services;
    }

    /// <summary>
    /// The autonomous worker (ADR-0037). Off unless <c>AutonomousWorker:Enabled</c> says otherwise;
    /// the options bind regardless, because <see cref="HumanReviewGuard"/> reads the same policy to
    /// decide what an unattended agent may do, worker or not.
    /// </summary>
    public static IServiceCollection AddAutonomousWorker(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AutonomousWorkerOptions>()
            .Bind(configuration.GetSection(AutonomousWorkerOptions.SectionName))
            // Before validation, and before anything reads the policy. The collections bind into an
            // empty list so configuration replaces rather than appends; this puts the defaults back
            // where configuration stayed silent.
            .PostConfigure(o => o.ApplyDefaults())
            .Validate(o => o.ScanIntervalSeconds is >= 10 and <= 86_400,
                "AutonomousWorker:ScanIntervalSeconds must be between 10 and 86400.")
            .Validate(o => o.MaxTicketsPerRun is > 0 and <= 100,
                "AutonomousWorker:MaxTicketsPerRun must be between 1 and 100.")
            .Validate(o => o.MinTicketAgeMinutes >= 0,
                "AutonomousWorker:MinTicketAgeMinutes must not be negative.")
            .Validate(o => o.ScanStatuses.Count > 0,
                "AutonomousWorker:ScanStatuses must name at least one status, or the worker scans nothing.")
            .Validate(o => o.AutonomousWrites.All(ToolCatalog.IsKnown),
                "AutonomousWorker:AutonomousWrites may only name tools that exist. "
                + "A misspelt name would silently grant nothing.")
            .Validate(o => !o.ScanStatuses.Contains(Domain.Tickets.TicketStatus.Closed),
                "AutonomousWorker:ScanStatuses must not include Closed — the worker would reopen "
                + "finished work on every scan.")
            .ValidateOnStart();

        // Scoped: the worker resolves one per ticket, inside the scope it made autonomous.
        // Registered even when the worker is disabled, so the reviewer stays directly testable.
        services.AddScoped<TicketReviewer>();
        services.AddHostedService<TicketWorker>();

        return services;
    }

    private static bool IsValidTimeZone(string id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}
