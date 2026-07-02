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
    private const string TestApiKey = "test-api-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "InMemory",
                ["Auth:ApiKey"] = TestApiKey,
                // Satisfies the assistant options' fail-fast validation; tests never call Anthropic
                ["Anthropic:ApiKey"] = "test-anthropic-key"
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

    protected override void ConfigureClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("X-Api-Key", TestApiKey);
    }
}
