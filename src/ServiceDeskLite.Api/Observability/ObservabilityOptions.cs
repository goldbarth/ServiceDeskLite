namespace ServiceDeskLite.Api.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>Serves the Prometheus scrape endpoint at <see cref="MetricsPath"/>.</summary>
    public bool PrometheusEnabled { get; init; } = true;

    /// <summary>
    /// Unauthenticated by design: a scraper is infrastructure, not a client, and the endpoint
    /// exposes counters rather than ticket data. Expose it on an internal network, or turn it off.
    /// </summary>
    public string MetricsPath { get; init; } = "/metrics";

    /// <summary>
    /// OTLP collector for traces, e.g. <c>http://localhost:4317</c>. Empty means no exporter is
    /// registered: the spans are still created, cost close to nothing without a listener, and can
    /// be turned on later without touching code.
    /// </summary>
    public string OtlpEndpoint { get; init; } = string.Empty;

    public string ServiceName { get; init; } = "servicedesklite-api";
}
