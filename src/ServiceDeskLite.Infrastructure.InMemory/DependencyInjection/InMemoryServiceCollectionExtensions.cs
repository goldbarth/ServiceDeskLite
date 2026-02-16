using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Infrastructure.InMemory.Persistence;

namespace ServiceDeskLite.Infrastructure.InMemory.DependencyInjection;

public static class InMemoryServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureInMemory(this IServiceCollection services)
    {
        // Store: Singleton, so that data persists across requests (useful for UI tests)
        services.AddSingleton<InMemoryStore>();
        
        // UoW + Repo: Scoped -> separate ChangeSet for each request/use case
        services.AddScoped<InMemoryUnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<InMemoryUnitOfWork>());

        services.AddScoped<ITicketRepository, InMemoryTicketRepository>();
        
        return services;
    } 
}
