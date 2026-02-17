using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ServiceDeskLite.Tests.Api.Infrastructure;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public InMemorySink Sink { get; } = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        
        // Override Serilog for the test host + add capture sink
        builder.UseSerilog((ctx, services, cfg) =>
        {
            cfg
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.Sink(Sink);
        });
        
        return base.CreateHost(builder);
    }
}
