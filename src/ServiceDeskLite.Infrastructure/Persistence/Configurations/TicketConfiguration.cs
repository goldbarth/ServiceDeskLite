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
            .HasMaxLength(Ticket.MaxTitleLength);

        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(Ticket.MaxDescriptionLength);

        builder.Property(t => t.Priority)
            .IsRequired();

        builder.Property(t => t.Status)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.DueAt)
            .IsRequired(false);

        builder.Property(t => t.Assignee)
            .HasConversion(
                v => v.HasValue ? v.Value.Name : null,
                v => v != null ? new Assignee(v) : (Assignee?)null)
            .HasMaxLength(Assignee.MaxNameLength)
            .IsRequired(false);

        // Indices for search/paging
        builder.HasIndex(t => t.CreatedAt);
        builder.HasIndex(t => t.Status);

        // Comments are part of the Ticket aggregate – owned entity, separate table.
        // EF accesses the private _comments backing field to populate the collection.
        builder.Navigation(t => t.Comments).HasField("_comments");
        builder.OwnsMany(t => t.Comments, comment =>
        {
            comment.ToTable("TicketComments");

            comment.HasKey(c => c.Id);

            comment.Property(c => c.Id)
                .HasConversion(v => v.Value, v => new CommentId(v))
                .ValueGeneratedNever();

            comment.Property(c => c.Content)
                .IsRequired()
                .HasMaxLength(Comment.MaxContentLength);

            comment.Property(c => c.Author)
                .HasMaxLength(Comment.MaxAuthorLength)
                .IsRequired(false);

            comment.Property(c => c.CreatedAt)
                .IsRequired();

            comment.HasIndex(c => c.CreatedAt);
        });
    }
}
