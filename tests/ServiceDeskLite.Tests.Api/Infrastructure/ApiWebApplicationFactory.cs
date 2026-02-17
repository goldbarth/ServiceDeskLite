using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

using Serilog;

namespace ServiceDeskLite.Tests.Api.Infrastructure;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public InMemorySink Sink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

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
