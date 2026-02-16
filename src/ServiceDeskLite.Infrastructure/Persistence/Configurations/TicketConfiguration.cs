using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(new TicketIdConverter())
            .ValueGeneratedNever();
        
        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(t => t.Priority)
            .IsRequired();
        
        builder.Property(t => t.Status)
            .IsRequired();
        
        builder.Property(t => t.CreatedAt)
            .IsRequired();
        
        builder.Property(t => t.DueAt)
            .IsRequired();
        
        // Indices for search/paging
        builder.HasIndex(t => t.CreatedAt);
        builder.HasIndex(t => t.Status);
    }
}
