using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Web.Api.V1;

namespace ServiceDeskLite.Web.Composition;

public static class ApiClientComposition
{
    public static IServiceCollection AddTicketsApiClient(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiClientOptions>(
            configuration.GetSection("ApiClient"));

        services.AddHttpClient<ITicketsApiClient, TicketsApiClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<ApiClientOptions>>().Value;

            if (string.IsNullOrWhiteSpace(opt.BaseUrl))
                throw new InvalidOperationException("ApiClient:BaseUrl must be configured.");

            http.BaseAddress = new Uri(opt.BaseUrl, UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);

            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }
}
