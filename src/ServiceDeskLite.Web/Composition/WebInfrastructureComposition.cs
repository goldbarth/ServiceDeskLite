using ServiceDeskLite.Infrastructure.InMemory.DependencyInjection;
using ServiceDeskLite.Infrastructure.Persistence.DependencyInjection;

namespace ServiceDeskLite.Web.Composition;

public static class WebInfrastructureComposition
{
    public static IServiceCollection AddWebInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Persistence:Provider"] ?? "Sqlite";

        switch (provider)
        {
            case "InMemory":
                services.AddInfrastructureInMemory();
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
