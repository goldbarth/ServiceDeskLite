using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceDeskLite.Infrastructure.Persistence.AssistantMetrics;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

public sealed class AssistantToolInvocationConfiguration : IEntityTypeConfiguration<AssistantToolInvocationRecord>
{
    public void Configure(EntityTypeBuilder<AssistantToolInvocationRecord> builder)
    {
        builder.ToTable("AssistantToolInvocations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.ToolName)
            .IsRequired()
            .HasMaxLength(100);

        // Stored as text: a reordered enum must not silently remap historical rows.
        builder.Property(i => i.Kind)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(i => i.OccurredAt)
            .IsRequired();

        // Every read is a trailing window grouped by tool.
        builder.HasIndex(i => i.OccurredAt);
    }
}

public sealed class AssistantTokenUsageConfiguration : IEntityTypeConfiguration<AssistantTokenUsageRecord>
{
    public void Configure(EntityTypeBuilder<AssistantTokenUsageRecord> builder)
    {
        builder.ToTable("AssistantTokenUsages");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Model)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.OccurredAt)
            .IsRequired();

        builder.HasIndex(u => u.OccurredAt);
    }
}
