namespace ServiceDeskLite.Web.Api.V1;

/// <summary>
/// Attaches the configured API key to every outbound HTTP request.
/// The key is read from Auth:ApiKey at the time of the request so that
/// configuration changes (e.g. via environment variables) take effect
/// without restarting the application.
/// </summary>
internal sealed class ApiKeyDelegatingHandler(IConfiguration configuration) : DelegatingHandler
{
    private const string HeaderName = "X-Api-Key";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var apiKey = configuration["Auth:ApiKey"];

        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.TryAddWithoutValidation(HeaderName, apiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
