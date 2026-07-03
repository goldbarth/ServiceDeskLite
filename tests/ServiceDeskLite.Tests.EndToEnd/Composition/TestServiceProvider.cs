using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.DependencyInjection;
using ServiceDeskLite.Infrastructure.InMemory.DependencyInjection;
using ServiceDeskLite.Infrastructure.Persistence;
using ServiceDeskLite.Infrastructure.Persistence.DependencyInjection;

using Testcontainers.PostgreSql;

namespace ServiceDeskLite.Tests.EndToEnd.Composition;

public enum PersistenceProvider
{
    InMemory,
    Postgres
}

public sealed class TestServiceProvider : IAsyncDisposable
{
    private readonly ServiceProvider _root;
    private readonly PostgreSqlContainer? _container;

    private TestServiceProvider(ServiceProvider root, PostgreSqlContainer? container = null)
    {
        _root = root;
        _container = container;
    }

    public IServiceScope CreateScope() => _root.CreateScope();

    public static async Task<TestServiceProvider> CreateAsync(PersistenceProvider provider)
    {
        var services = new ServiceCollection();
        services.AddApplication();

        PostgreSqlContainer? container = null;

        switch (provider)
        {
            case PersistenceProvider.InMemory:
                services.AddInfrastructureInMemory();
                break;

            case PersistenceProvider.Postgres:
                // pgvector image: the TicketEmbeddings migration runs CREATE EXTENSION
                // vector, which plain postgres:17 does not ship. Mirrors docker-compose.
                container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
                    .WithDatabase("servicedesklite")
                    .WithUsername("postgres")
                    .WithPassword("postgres")
                    .Build();

                await container.StartAsync();

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:ServiceDeskLite"] = container.GetConnectionString()
                    })
                    .Build();

                services.AddInfrastructure(config);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(provider));
        }

        var root = services.BuildServiceProvider(validateScopes: true);

        if (provider == PersistenceProvider.Postgres)
        {
            using var scope = root.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ServiceDeskLiteDbContext>();
            await db.Database.MigrateAsync();
        }

        return new TestServiceProvider(root, container);
    }

    public async ValueTask DisposeAsync()
    {
        await _root.DisposeAsync();

        if (_container is not null)
            await _container.DisposeAsync();
    }
}
