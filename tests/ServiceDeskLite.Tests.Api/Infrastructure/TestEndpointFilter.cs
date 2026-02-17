using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ServiceDeskLite.Tests.Api.Infrastructure;

/// <summary>
/// IStartupFilter that appends test middleware after the main pipeline.
/// Exceptions propagate back through the full middleware chain (incl. ExceptionHandler).
/// </summary>
public sealed class TestEndpointFilter : IStartupFilter
{
    private readonly Action<IApplicationBuilder> _configure;

    public TestEndpointFilter(Action<IApplicationBuilder> configure)
        => _configure = configure;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            next(app);       // build main pipeline first
            _configure(app); // append test middleware after
        };
    }
}
