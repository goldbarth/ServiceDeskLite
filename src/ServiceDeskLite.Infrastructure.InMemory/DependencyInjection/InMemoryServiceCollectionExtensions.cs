using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Infrastructure.InMemory.Persistence;
using ServiceDeskLite.Infrastructure.InMemory.Search;

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
        services.AddScoped<IAgentRepository, InMemoryAgentRepository>();
        services.AddScoped<IAuditEventRepository, InMemoryAuditEventRepository>();
        services.AddScoped<IOutboxRepository, InMemoryOutboxRepository>();
        services.AddScoped<IDashboardRepository, InMemoryDashboardRepository>();

        services.AddScoped<ITicketSimilaritySearch, UnavailableTicketSimilaritySearch>();

        services.AddScoped<IConversationStore, InMemoryConversationStore>();

        services.AddScoped<UnavailableMemoryStore>();
        services.AddScoped<IMemoryStore>(sp => sp.GetRequiredService<UnavailableMemoryStore>());
        services.AddScoped<IMemorySearch>(sp => sp.GetRequiredService<UnavailableMemoryStore>());

        return services;
    } 
}
