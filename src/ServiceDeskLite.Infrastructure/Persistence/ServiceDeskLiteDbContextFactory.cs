using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

using Pgvector.EntityFrameworkCore;

namespace ServiceDeskLite.Infrastructure.Persistence;

public class ServiceDeskLiteDbContextFactory : IDesignTimeDbContextFactory<ServiceDeskLiteDbContext>
{
    public ServiceDeskLiteDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ServiceDeskLiteDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=servicedesklite;Username=postgres;Password=postgres",
                npgsql => npgsql.UseVector())
            .Options;

        return new ServiceDeskLiteDbContext(options);
    }
}
