using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDeskLite.Domain.Audit;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasConversion(new AuditEventIdConverter())
            .ValueGeneratedNever();

        // TicketId stored as Guid — no FK constraint intentionally:
        // audit events should survive even if the ticket is ever deleted (historical record).
        builder.Property(e => e.TicketId)
            .HasConversion(new TicketIdConverter())
            .IsRequired();

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(AuditEvent.MaxEventTypeLength);

        builder.Property(e => e.Actor)
            .HasMaxLength(AuditEvent.MaxActorLength)
            .IsRequired(false);

        builder.Property(e => e.OccurredAt)
            .IsRequired();

        // Payload is a JSON string — no length limit, stored as TEXT.
        builder.Property(e => e.Payload)
            .IsRequired();

        // Primary query: all events for a ticket, ordered by time.
        builder.HasIndex(e => e.TicketId);
        builder.HasIndex(e => e.OccurredAt);
    }
}
