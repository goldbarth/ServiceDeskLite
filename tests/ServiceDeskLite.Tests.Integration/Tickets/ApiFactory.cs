using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Infrastructure.InMemory.DependencyInjection;
using ServiceDeskLite.Infrastructure.Persistence;

namespace ServiceDeskLite.Tests.Integration.Tickets;

public class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "InMemory"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ServiceDeskLiteDbContext>();
            services.RemoveAll<ITicketRepository>();
            services.RemoveAll<IAuditEventRepository>();
            services.RemoveAll<IUnitOfWork>();

            services.AddInfrastructureInMemory();
        });
    }
}
