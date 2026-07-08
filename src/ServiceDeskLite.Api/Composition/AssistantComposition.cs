using Anthropic;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Api.Assistant;
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

        services.AddSingleton<ICurrentUser, DemoCurrentUser>();

        services.AddScoped<CreateTicketTool>();
        services.AddScoped<UpdateTicketTool>();
        services.AddScoped<FindSimilarTicketsTool>();
        services.AddScoped<SearchTicketsTool>();
        services.AddScoped<ChangeTicketStatusTool>();
        services.AddScoped<AssignTicketTool>();
        services.AddScoped<RememberTool>();
        services.AddScoped<RecallMemoryTool>();
        services.AddScoped<AssistantChatService>();

        return services;
    }

    private static bool IsValidTimeZone(string id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}
