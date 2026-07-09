using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ServiceDeskLite.Api.Composition;

/// <summary>
/// Wraps an already-registered service in a decorator, keeping the original lifetime.
/// </summary>
/// <remarks>
/// A hand-rolled six-liner instead of a dependency on Scrutor: this is the only decoration in the
/// composition root. The inner registrations live in the persistence adapters and are internal
/// types, so the decorator has to be built from the existing descriptor rather than from a name.
/// </remarks>
public static class ServiceCollectionDecorationExtensions
{
    public static IServiceCollection Decorate<TService, TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        var inner = services.LastOrDefault(d => d.ServiceType == typeof(TService))
            ?? throw new InvalidOperationException(
                $"Cannot decorate {typeof(TService).Name}: it is not registered. "
                + "Register the persistence adapter before decorating it.");

        services.Remove(inner);

        services.Add(new ServiceDescriptor(
            typeof(TService),
            sp => ActivatorUtilities.CreateInstance<TDecorator>(sp, CreateInner(sp, inner)),
            inner.Lifetime));

        return services;
    }

    private static object CreateInner(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is { } instance)
            return instance;

        if (descriptor.ImplementationFactory is { } factory)
            return factory(sp);

        return ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
    }
}
