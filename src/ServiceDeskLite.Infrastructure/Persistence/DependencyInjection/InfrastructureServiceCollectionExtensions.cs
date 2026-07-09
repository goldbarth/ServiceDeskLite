using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Pgvector.EntityFrameworkCore;

using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Infrastructure.Embeddings;
using ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;
using ServiceDeskLite.Infrastructure.Persistence.AssistantMetrics;
using ServiceDeskLite.Infrastructure.Persistence.Conversations;
using ServiceDeskLite.Infrastructure.Persistence.Repositories;
using ServiceDeskLite.Infrastructure.Persistence.UnitOfWork;

namespace ServiceDeskLite.Infrastructure.Persistence.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ServiceDeskLiteDbContext>(opt =>
            opt.UseNpgsql(
                configuration.GetConnectionString("ServiceDeskLite"),
                npgsql => npgsql.UseVector()));
        
        services.AddScoped<ITicketRepository, EfTicketRepository>();
        services.AddScoped<IAgentRepository, EfAgentRepository>();
        services.AddScoped<IAuditEventRepository, EfAuditEventRepository>();
        services.AddScoped<IOutboxRepository, EfOutboxRepository>();
        services.AddScoped<IDashboardRepository, EfDashboardRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<IAiDashboardRepository, EfAiDashboardRepository>();

        services.AddScoped<IConversationStore, EfConversationStore>();

        // Singleton: the sink opens its own scope per write and must not hold the
        // request's DbContext (see EfAssistantMetricsSink).
        services.AddSingleton<IAssistantMetricsSink, EfAssistantMetricsSink>();

        services.AddTicketEmbeddings(configuration);

        return services;
    }

    /// <summary>
    /// Semantic ticket search (RAG): Voyage embedding client, background
    /// embedding worker, pgvector similarity search. Postgres-only — the
    /// InMemory provider registers an unavailable stand-in instead.
    /// </summary>
    private static IServiceCollection AddTicketEmbeddings(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<VoyageOptions>()
            .Bind(configuration.GetSection(VoyageOptions.SectionName))
            .Validate(o => o.Dimensions == Configurations.TicketEmbeddingConfiguration.Dimensions,
                $"Voyage:Dimensions must match the vector column width " +
                $"({Configurations.TicketEmbeddingConfiguration.Dimensions}); changing it requires a migration and re-embedding.")
            .Validate(o => o.BatchSize is > 0 and <= 128,
                "Voyage:BatchSize must be between 1 and 128.")
            .Validate(o => o.PollSeconds is >= 1 and <= 3600,
                "Voyage:PollSeconds must be between 1 and 3600.")
            .ValidateOnStart();

        services.AddHttpClient<IEmbeddingClient, VoyageEmbeddingClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<VoyageOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl);
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
        });

        services.AddScoped<ITicketSimilaritySearch, PgVectorTicketSimilaritySearch>();
        services.AddHostedService<TicketEmbeddingWorker>();

        // Knowledge-base RAG: file corpus → chunked embeddings (worker) → pgvector search.
        // Shares the Voyage client and its enabled/disabled gate with ticket search.
        services.AddOptions<KnowledgeBaseOptions>()
            .Bind(configuration.GetSection(KnowledgeBaseOptions.SectionName));
        services.AddSingleton<IKnowledgeCorpus, FileKnowledgeCorpus>();
        services.AddScoped<IKnowledgeBaseSearch, PgVectorKnowledgeBaseSearch>();
        services.AddHostedService<KnowledgeEmbeddingWorker>();

        // Long-term memory shares the Voyage embedding client; one instance backs both ports.
        services.AddScoped<PgVectorMemoryStore>();
        services.AddScoped<IMemoryStore>(sp => sp.GetRequiredService<PgVectorMemoryStore>());
        services.AddScoped<IMemorySearch>(sp => sp.GetRequiredService<PgVectorMemoryStore>());

        return services;
    }
}
