using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceDeskLite.Domain.Agents;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(new AgentIdConverter())
            .ValueGeneratedNever();

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(Agent.MaxNameLength);

        builder.Property(a => a.Email)
            .IsRequired()
            .HasMaxLength(Agent.MaxEmailLength);

        builder.Property(a => a.Active)
            .IsRequired();

        builder.HasIndex(a => a.Active);
    }
}
