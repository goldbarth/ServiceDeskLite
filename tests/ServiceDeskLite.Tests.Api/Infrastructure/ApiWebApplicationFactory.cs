using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

using Serilog;

namespace ServiceDeskLite.Tests.Api.Infrastructure;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public InMemorySink Sink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

        builder.ConfigureServices(services =>
        {
            services.AddSerilog(cfg =>
            {
                cfg
                    .MinimumLevel.Debug()
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.Sink(Sink);
            });
        });
    }
}
