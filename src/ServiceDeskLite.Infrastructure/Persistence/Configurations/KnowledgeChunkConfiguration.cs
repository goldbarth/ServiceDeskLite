using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

public class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        builder.ToTable("KnowledgeChunks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.ArticleId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Source)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Heading)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Ordinal)
            .IsRequired();

        builder.Property(c => c.Content)
            .IsRequired();

        builder.Property(c => c.ContentHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(c => c.Model)
            .IsRequired()
            .HasMaxLength(100);

        // Same width as ticket embeddings — must match VoyageOptions.Dimensions.
        builder.Property(c => c.Vector)
            .HasColumnType($"vector({TicketEmbeddingConfiguration.Dimensions})")
            .IsRequired();

        builder.Property(c => c.EmbeddedAt)
            .IsRequired();

        // Grouping/cleanup by article (worker deletes an article's chunks when it leaves the corpus).
        builder.HasIndex(c => c.ArticleId);

        // No vector index (HNSW/IVFFlat) on purpose: sequential scan is exact and
        // fast at corpus scale, same reasoning as TicketEmbeddings.
    }
}
