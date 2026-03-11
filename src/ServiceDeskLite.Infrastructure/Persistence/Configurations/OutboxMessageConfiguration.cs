using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDeskLite.Domain.Outbox;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasConversion(new OutboxMessageIdConverter())
            .ValueGeneratedNever();

        builder.Property(m => m.EventType)
            .IsRequired()
            .HasMaxLength(OutboxMessage.MaxEventTypeLength);

        // Payload is a JSON string - no length limit, stored as TEXT.
        builder.Property(m => m.Payload)
            .IsRequired();

        builder.Property(m => m.OccurredAt)
            .IsRequired();

        // Null means Pending; a timestamp means Dispatched.
        builder.Property(m => m.DispatchedAt)
            .IsRequired(false);

        // Status is computed from DispatchedAt - not a stored column.
        builder.Ignore(m => m.Status);

        // The background dispatcher polls for Pending messages ordered by time.
        builder.HasIndex(m => m.OccurredAt);
        // Allows efficient filtering: WHERE DispatchedAt IS NULL.
        builder.HasIndex(m => m.DispatchedAt);
    }
}
