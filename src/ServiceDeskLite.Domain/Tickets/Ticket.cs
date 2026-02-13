using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets;

public sealed class Ticket
{
    public TicketId Id { get; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? DueAt { get; private set; }

    public Ticket(
        TicketId id,
        string title,
        string description,
        TicketPriority priority,
        DateTimeOffset createdAt,
        DateTimeOffset? dueAt = null)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, 200, nameof(title));
        Guard.NotNullOrWhiteSpace(description, nameof(description));
        Guard.MaxLength(description, 2000, nameof(description));

        Id = id;
        Title = title;
        Description = description;
        Priority = priority;
        Status = TicketStatus.New;
        CreatedAt = createdAt;
        DueAt = dueAt;
    }

    public void ChangeStatus(TicketStatus newStatus)
    {
        TicketWorkflow.EnsureCanTransition(Status, newStatus);

        Status = newStatus;
        
        // ChangedAt
    }
}
