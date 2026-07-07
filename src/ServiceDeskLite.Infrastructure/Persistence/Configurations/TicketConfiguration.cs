using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using ServiceDeskLite.Domain.Agents;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    /// <summary>
    /// Shadow computed column holding the display-ref suffix (last 6 hex chars of the id,
    /// lower-cased) so tickets can be looked up by reference number in translatable SQL.
    /// </summary>
    public const string RefSuffixColumn = "RefSuffix";

    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(t => t.Id);

        builder.Property<string>(RefSuffixColumn)
            .HasComputedColumnSql("right(\"Id\"::text, 6)", stored: true);
        builder.HasIndex(RefSuffixColumn);

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

        // FK to the assigned agent (ADR-0025); null = unassigned. Stored as the agent's
        // Guid via the strongly-typed-id converter. Modelled as a scalar reference (no
        // navigation) — the roster is a separate aggregate; the display name is joined
        // on read and the audit trail snapshots the name independently.
        builder.Property(t => t.AssignedAgentId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (Guid?)null,
                v => v.HasValue ? new AgentId(v.Value) : (AgentId?)null)
            .IsRequired(false);

        // Indices for search/paging
        builder.HasIndex(t => t.CreatedAt);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.AssignedAgentId);

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
