using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        // SQLite does not support DateTimeOffset in ORDER BY — store as ticks (long)
        var dtoConverter = new DateTimeOffsetToBinaryConverter();

        builder.Property(t => t.CreatedAt)
            .HasConversion(dtoConverter)
            .IsRequired();

        builder.Property(t => t.DueAt)
            .HasConversion(new ValueConverter<DateTimeOffset?, long?>(
                v => v.HasValue ? v.Value.ToUniversalTime().Ticks : null,
                v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null))
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
                .HasConversion(dtoConverter)
                .IsRequired();

            comment.HasIndex(c => c.CreatedAt);
        });
    }
}
