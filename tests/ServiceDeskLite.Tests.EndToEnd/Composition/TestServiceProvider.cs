using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.DependencyInjection;
using ServiceDeskLite.Infrastructure.InMemory.DependencyInjection;
using ServiceDeskLite.Infrastructure.Persistence;
using ServiceDeskLite.Infrastructure.Persistence.DependencyInjection;

namespace ServiceDeskLite.Tests.EndToEnd.Composition;

public enum PersistenceProvider
{
    InMemory,
    Sqlite
}

public sealed class TestServiceProvider : IDisposable
{
    private readonly ServiceProvider _root;
    private readonly SqliteConnection? _keepAlive;

    private TestServiceProvider(ServiceProvider root, SqliteConnection? keepAlive = null)
    {
        _root = root;
        _keepAlive = keepAlive;
    }

    public IServiceScope CreateScope() => _root.CreateScope();

    public static TestServiceProvider Create(PersistenceProvider provider)
    {
        var services = new ServiceCollection();
        services.AddApplication();

        SqliteConnection? keepAlive = null;

        switch (provider)
        {
            case PersistenceProvider.InMemory:
                services.AddInfrastructureInMemory();
                break;

            case PersistenceProvider.Sqlite:
                // Shared in-memory SQLite — survives across scopes, no file cleanup
                var connString = $"Data Source=e2e_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
                keepAlive = new SqliteConnection(connString);
                keepAlive.Open();

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:ServiceDeskLite"] = connString
                    })
                    .Build();
                services.AddInfrastructure(config);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(provider));
        }

        var root = services.BuildServiceProvider(validateScopes: true);

        if (provider == PersistenceProvider.Sqlite)
        {
            using var scope = root.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ServiceDeskLiteDbContext>();
            db.Database.EnsureCreated();
        }

        return new TestServiceProvider(root, keepAlive);
    }

    public void Dispose()
    {
        _root.Dispose();
        _keepAlive?.Dispose();
    }
}
