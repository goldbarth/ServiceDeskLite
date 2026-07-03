using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceDeskLite.Infrastructure.Embeddings;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

public class TicketEmbeddingConfiguration : IEntityTypeConfiguration<TicketEmbedding>
{
    /// <summary>Must match VoyageOptions.Dimensions; changing it requires a migration + re-embed.</summary>
    public const int Dimensions = 1024;

    public void Configure(EntityTypeBuilder<TicketEmbedding> builder)
    {
        builder.ToTable("TicketEmbeddings");

        builder.HasKey(e => e.TicketId);

        builder.Property(e => e.TicketId)
            .HasConversion(new TicketIdConverter())
            .ValueGeneratedNever();

        // FK with cascade delete: an embedding without its ticket is meaningless.
        builder.HasOne<Domain.Tickets.Ticket>()
            .WithOne()
            .HasForeignKey<TicketEmbedding>(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.Vector)
            .HasColumnType($"vector({Dimensions})")
            .IsRequired();

        builder.Property(e => e.ContentHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.Model)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.EmbeddedAt)
            .IsRequired();

        // No vector index (HNSW/IVFFlat) on purpose: at ServiceDeskLite scale a
        // sequential scan is fast and exact; an index would add tuning surface.
    }
}
