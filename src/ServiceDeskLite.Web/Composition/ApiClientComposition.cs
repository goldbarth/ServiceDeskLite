using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Web.Api.V1;
using ServiceDeskLite.Web.Api.V1.Assistant;

namespace ServiceDeskLite.Web.Composition;

public static class ApiClientComposition
{
    public static IServiceCollection AddTicketsApiClient(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiClientOptions>(
            configuration.GetSection("ApiClient"));

        services.AddTransient<ApiKeyDelegatingHandler>();

        services.AddHttpClient<ITicketsApiClient, TicketsApiClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<ApiClientOptions>>().Value;

            if (string.IsNullOrWhiteSpace(opt.BaseUrl))
                throw new InvalidOperationException("ApiClient:BaseUrl must be configured.");

            http.BaseAddress = new Uri(opt.BaseUrl, UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);

            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        })
        .AddHttpMessageHandler<ApiKeyDelegatingHandler>();

        return services;
    }

    public static IServiceCollection AddAssistantApiClient(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiClientOptions>(
            configuration.GetSection("ApiClient"));

        services.AddTransient<ApiKeyDelegatingHandler>();

        services.AddHttpClient<IAssistantApiClient, AssistantApiClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<ApiClientOptions>>().Value;

            if (string.IsNullOrWhiteSpace(opt.BaseUrl))
                throw new InvalidOperationException("ApiClient:BaseUrl must be configured.");

            http.BaseAddress = new Uri(opt.BaseUrl, UriKind.Absolute);

            // SSE responses stay open while the model streams; the regular
            // request timeout would cut them off. Cancellation comes from the caller.
            http.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddHttpMessageHandler<ApiKeyDelegatingHandler>();

        return services;
    }
}
