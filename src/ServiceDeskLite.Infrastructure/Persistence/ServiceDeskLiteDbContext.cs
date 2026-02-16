using Microsoft.EntityFrameworkCore;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.Persistence;

public class ServiceDeskLiteDbContext(DbContextOptions options) 
    : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceDeskLiteDbContext).Assembly);
    }
}
