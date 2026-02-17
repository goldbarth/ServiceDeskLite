using System.Diagnostics;

namespace ServiceDeskLite.Api.Http.Observability;

public static class Correlation
{
    public static string GetTraceId(HttpContext ctx)
        => Activity.Current?.Id
           ?? ctx.TraceIdentifier
           ?? "unknown";
}
