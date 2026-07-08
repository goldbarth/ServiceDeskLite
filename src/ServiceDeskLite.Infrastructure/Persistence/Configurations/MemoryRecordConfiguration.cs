using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceDeskLite.Infrastructure.Embeddings;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

public class MemoryRecordConfiguration : IEntityTypeConfiguration<MemoryRecord>
{
    public void Configure(EntityTypeBuilder<MemoryRecord> builder)
    {
        builder.ToTable("Memories");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion(new MemoryIdConverter())
            .ValueGeneratedNever();

        builder.Property(m => m.Owner)
            .HasConversion(new OwnerIdConverter())
            .IsRequired();

        builder.Property(m => m.Content)
            .IsRequired();

        builder.Property(m => m.Kind)
            .IsRequired()
            .HasMaxLength(32);

        // Same width as ticket embeddings — must match VoyageOptions.Dimensions.
        builder.Property(m => m.Vector)
            .HasColumnType($"vector({TicketEmbeddingConfiguration.Dimensions})")
            .IsRequired();

        builder.Property(m => m.Model)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        // Recall is always owner-scoped.
        builder.HasIndex(m => m.Owner);
    }
}
