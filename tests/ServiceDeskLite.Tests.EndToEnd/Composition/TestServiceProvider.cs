using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.DependencyInjection;
using ServiceDeskLite.Infrastructure.InMemory.DependencyInjection;

namespace ServiceDeskLite.Tests.EndToEnd.Composition;

public enum PersistenceProvider
{
    InMemory
}

public sealed class TestServiceProvider : IDisposable
{
    private readonly ServiceProvider _root;

    private TestServiceProvider(ServiceProvider root) => _root = root;

    public IServiceScope CreateScope() => _root.CreateScope();

    public static TestServiceProvider Create(PersistenceProvider provider)
    {
        var services = new ServiceCollection();
        services.AddApplication();

        switch (provider)
        {
            case PersistenceProvider.InMemory:
                services.AddInfrastructureInMemory();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(provider));
        }

        return new TestServiceProvider(services.BuildServiceProvider(validateScopes: true));
    }

    public void Dispose() => _root.Dispose();
}
