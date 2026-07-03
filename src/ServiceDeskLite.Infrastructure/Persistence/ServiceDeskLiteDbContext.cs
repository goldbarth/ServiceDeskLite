using Microsoft.EntityFrameworkCore;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Outbox;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Infrastructure.Embeddings;

namespace ServiceDeskLite.Infrastructure.Persistence;

public class ServiceDeskLiteDbContext(DbContextOptions options)
    : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<TicketEmbedding> TicketEmbeddings => Set<TicketEmbedding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // pgvector: created by migration; requires the pgvector/pgvector image.
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceDeskLiteDbContext).Assembly);
    }
}
