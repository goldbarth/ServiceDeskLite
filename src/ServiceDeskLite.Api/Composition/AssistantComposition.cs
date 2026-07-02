using Anthropic;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Api.Assistant;

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
            .Validate(o => IsValidTimeZone(o.UserTimeZone),
                "Anthropic:UserTimeZone must be a valid timezone id (e.g. Europe/Berlin).")
            .ValidateOnStart();

        services.AddSingleton(sp => new AnthropicClient
        {
            ApiKey = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value.ApiKey,
        });

        services.AddScoped<CreateTicketTool>();
        services.AddScoped<UpdateTicketTool>();
        services.AddScoped<AssistantChatService>();

        return services;
    }

    private static bool IsValidTimeZone(string id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}
