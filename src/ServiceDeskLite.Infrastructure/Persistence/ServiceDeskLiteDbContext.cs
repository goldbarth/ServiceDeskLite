using Microsoft.EntityFrameworkCore;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Outbox;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.Persistence;

public class ServiceDeskLiteDbContext(DbContextOptions options)
    : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceDeskLiteDbContext).Assembly);
    }
}
