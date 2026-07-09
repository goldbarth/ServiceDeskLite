namespace ServiceDeskLite.Api.Http.Security;

/// <summary>
/// Demo-grade API key guard. Checks the X-Api-Key request header against
/// the configured Auth:ApiKey value. Returns 401 with no body on mismatch
/// to avoid leaking information. Swagger/OpenAPI paths are exempt so the
/// developer UI remains accessible without tooling changes.
/// </summary>
internal sealed class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string HeaderName = "X-Api-Key";
    private const string DefaultMetricsPath = "/metrics";

    public async Task InvokeAsync(HttpContext context)
    {
        // Swagger and OpenAPI endpoints are exempt from key checks.
        // They are only served in Development (see Program.cs), so this
        // does not open a gap in non-development environments.
        if (IsSwaggerPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        // The Prometheus scrape endpoint is exempt: a scraper is infrastructure, not a client,
        // and it would have to be handed the demo key to read counters that carry no ticket data.
        // Reachability is a deployment concern — bind it to an internal network, or disable it.
        if (context.Request.Path.StartsWithSegments(MetricsPath))
        {
            await next(context);
            return;
        }

        var expectedKey = configuration["Auth:ApiKey"];

        // No key configured → deny all. Misconfiguration must be visible
        // immediately rather than silently bypassed.
        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey)
            || !string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    private string MetricsPath
    {
        get
        {
            var configured = configuration["Observability:MetricsPath"];
            return string.IsNullOrWhiteSpace(configured) ? DefaultMetricsPath : configured;
        }
    }

    private static bool IsSwaggerPath(PathString path)
        => path.StartsWithSegments("/swagger")
        || path.StartsWithSegments("/openapi");
}
