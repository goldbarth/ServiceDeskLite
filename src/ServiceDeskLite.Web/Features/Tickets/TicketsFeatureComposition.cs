using ServiceDeskLite.Web.Features.Tickets.State;

namespace ServiceDeskLite.Web.Features.Tickets;

public static class TicketsFeatureComposition
{
    /// <summary>
    /// Registers Tickets feature services.
    /// <see cref="TicketsListFeatureState"/> is Scoped — one instance per SignalR circuit.
    /// </summary>
    public static IServiceCollection AddTicketsFeature(this IServiceCollection services)
    {
        services.AddScoped<TicketsListFeatureState>();
        return services;
    }
}
