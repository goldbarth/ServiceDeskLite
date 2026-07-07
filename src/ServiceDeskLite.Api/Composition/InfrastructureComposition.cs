using ServiceDeskLite.Application.Agents.Seeding;
using ServiceDeskLite.Application.Tickets.Seeding;
using ServiceDeskLite.Infrastructure.InMemory.DependencyInjection;
using ServiceDeskLite.Infrastructure.Persistence.DependencyInjection;
using ServiceDeskLite.Infrastructure.Persistence.Seeding;

namespace ServiceDeskLite.Api.Composition;

public static class InfrastructureComposition
{
    public static IServiceCollection AddApiInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Persistence:Provider"];

        switch (provider)
        {
            case "InMemory":
                services.AddInfrastructureInMemory();
                break;

            case "Postgres":
                services.AddInfrastructure(configuration);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown Persistence:Provider '{provider}'. Expected 'InMemory' or 'Postgres'.");
        }

        // Provider-agnostic (works via ITicketRepository) and idempotent — used
        // by the Development seeding block in Program.cs for both providers.
        services.AddScoped<ITicketSeeder, TicketSeeder>();
        services.AddScoped<IAgentSeeder, AgentSeeder>();

        return services;
    }
}
