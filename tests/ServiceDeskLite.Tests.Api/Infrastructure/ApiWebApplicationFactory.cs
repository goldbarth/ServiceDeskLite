using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

using Serilog;

namespace ServiceDeskLite.Tests.Api.Infrastructure;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public InMemorySink Sink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        // Override persistence to InMemory so API tests never require a real database
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "InMemory"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Sink(Sink)
                .CreateLogger();

            services.AddSerilog(logger, dispose: true);
        });
    }
}
