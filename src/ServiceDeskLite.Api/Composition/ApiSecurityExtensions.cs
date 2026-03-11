using ServiceDeskLite.Api.Http.Security;

namespace ServiceDeskLite.Api.Composition;

public static class ApiSecurityExtensions
{
    public static IApplicationBuilder UseApiSecurity(this IApplicationBuilder app)
        => app.UseMiddleware<ApiKeyMiddleware>();
}
