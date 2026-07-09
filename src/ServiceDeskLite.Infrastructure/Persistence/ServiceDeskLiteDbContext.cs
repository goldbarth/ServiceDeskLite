using Microsoft.EntityFrameworkCore;
using ServiceDeskLite.Domain.Agents;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Outbox;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Infrastructure.Embeddings;
using ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;
using ServiceDeskLite.Infrastructure.Persistence.AssistantMetrics;

namespace ServiceDeskLite.Infrastructure.Persistence;

public class ServiceDeskLiteDbContext(DbContextOptions options)
    : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<TicketEmbedding> TicketEmbeddings => Set<TicketEmbedding>();
    public DbSet<MemoryRecord> Memories => Set<MemoryRecord>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<AssistantToolInvocationRecord> AssistantToolInvocations => Set<AssistantToolInvocationRecord>();
    public DbSet<AssistantTokenUsageRecord> AssistantTokenUsages => Set<AssistantTokenUsageRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // pgvector: created by migration; requires the pgvector/pgvector image.
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceDeskLiteDbContext).Assembly);
    }
}
