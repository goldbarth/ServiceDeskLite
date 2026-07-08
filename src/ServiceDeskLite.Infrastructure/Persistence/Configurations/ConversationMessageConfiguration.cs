using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceDeskLite.Infrastructure.Persistence.Conversations;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

public class ConversationMessageConfiguration : IEntityTypeConfiguration<ConversationMessageRecord>
{
    public void Configure(EntityTypeBuilder<ConversationMessageRecord> builder)
    {
        builder.ToTable("ConversationMessages");

        // Sequence is dense per conversation; (conversation, sequence) is the natural key.
        builder.HasKey(m => new { m.ConversationId, m.Sequence });

        builder.Property(m => m.ConversationId)
            .HasConversion(new ConversationIdConverter());

        builder.Property(m => m.Owner)
            .HasConversion(new OwnerIdConverter())
            .IsRequired();

        builder.Property(m => m.Role)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(m => m.Content)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        // Load path is always owner + conversation scoped.
        builder.HasIndex(m => new { m.Owner, m.ConversationId });
    }
}
