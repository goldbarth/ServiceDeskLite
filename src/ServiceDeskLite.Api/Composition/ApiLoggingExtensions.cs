using Serilog;

using ServiceDeskLite.Api.Http.Observability;

namespace ServiceDeskLite.Api.Composition;

public static class ApiLoggingExtensions
{
    /// <summary>
    /// Registers Serilog request logging and enriches each request log entry
    /// with the correlation TraceId so every HTTP access log line is traceable.
    /// </summary>
    public static IApplicationBuilder UseApiRequestLogging(this IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging(opts =>
        {
            opts.EnrichDiagnosticContext = (diagCtx, httpCtx) =>
                diagCtx.Set("TraceId", Correlation.GetTraceId(httpCtx));
        });

        return app;
    }
}
