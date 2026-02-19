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
                services.AddScoped<ITicketSeeder, TicketSeeder>();
                break;

            case "Sqlite":
                services.AddInfrastructure(configuration);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown Persistence:Provider '{provider}'. Expected 'InMemory' or 'Sqlite'.");
        }

        return services;
    }
}
