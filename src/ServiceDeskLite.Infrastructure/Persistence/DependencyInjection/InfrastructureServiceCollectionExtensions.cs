using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Infrastructure.Persistence.Repositories;
using ServiceDeskLite.Infrastructure.Persistence.UnitOfWork;

namespace ServiceDeskLite.Infrastructure.Persistence.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ServiceDeskLiteDbContext>(opt =>
            opt.UseSqlite(configuration.GetConnectionString("ServiceDeskLite")));
        
        services.AddScoped<ITicketRepository, EfTicketRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        
        return services;
    }
}
