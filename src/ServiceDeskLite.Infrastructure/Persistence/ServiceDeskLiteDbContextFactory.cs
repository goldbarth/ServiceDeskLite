using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ServiceDeskLite.Infrastructure.Persistence;

public class ServiceDeskLiteDbContextFactory : IDesignTimeDbContextFactory<ServiceDeskLiteDbContext>
{
    public ServiceDeskLiteDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ServiceDeskLiteDbContext>()
            .UseSqlite("Data Source=servicedesklite.db")
            .Options;

        return new ServiceDeskLiteDbContext(options);
    }
}