using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using ServiceDeskLite.Api.Observability;
using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Api.Composition;

public static class ObservabilityComposition
{
    /// <summary>
    /// Metrics and traces for the assistant (ADR-0036). Instruments live on a BCL
    /// <c>Meter</c>/<c>ActivitySource</c>; only the exporters registered here decide who sees them.
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ObservabilityOptions>()
            .Bind(configuration.GetSection(ObservabilityOptions.SectionName))
            .Validate(o => o.MetricsPath.StartsWith('/'), "Observability:MetricsPath must start with '/'.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ServiceName), "Observability:ServiceName must not be empty.")
            .ValidateOnStart();

        var options = configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>()
            ?? new ObservabilityOptions();

        services.AddSingleton<AssistantInstrumentation>();

        // The agent records each signal once; the decorator fans it out to the instruments and
        // then to the persisting sink, so Prometheus and the AI dashboard cannot drift apart.
        services.Decorate<IAssistantMetricsSink, MeterAssistantMetricsSink>();

        var otel = services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(options.ServiceName))
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(AssistantInstrumentation.MeterName);
                metrics.AddAspNetCoreInstrumentation();

                // The default boundaries (0, 5, 10, ... 10000) are meant for millisecond latency,
                // so a 0..1 confidence would pile into the first bucket and the histogram would
                // answer nothing. Give each histogram boundaries matched to what it actually holds.
                metrics.AddView(
                    AssistantInstrumentation.RetrievalConfidenceName,
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0],
                    });
                metrics.AddView(
                    AssistantInstrumentation.ToolDurationName,
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000],
                    });

                if (options.PrometheusEnabled)
                    metrics.AddPrometheusExporter();
            });

        otel.WithTracing(tracing =>
        {
            tracing.AddSource(AssistantInstrumentation.ActivitySourceName);
            tracing.AddAspNetCoreInstrumentation();

            // No endpoint, no exporter. The spans still exist; without a listener they cost
            // almost nothing, and enabling collection later is a configuration change.
            if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));
        });

        return services;
    }

    /// <summary>Maps the Prometheus scrape endpoint, if it is enabled.</summary>
    public static WebApplication UseObservability(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

        if (options.PrometheusEnabled)
            app.MapPrometheusScrapingEndpoint(options.MetricsPath);

        return app;
    }
}
