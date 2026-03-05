using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets;

public sealed class Ticket
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;

    public TicketId Id { get; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? DueAt { get; private set; }
    public Assignee? Assignee { get; private set; }

    private readonly List<Comment> _comments = [];
    public IReadOnlyList<Comment> Comments => _comments.AsReadOnly();

    public Ticket(
        TicketId id,
        string title,
        string description,
        TicketPriority priority,
        DateTimeOffset createdAt,
        DateTimeOffset? dueAt = null)
    {
        Guard.NotNullOrWhiteSpace(title, nameof(title));
        Guard.MaxLength(title, MaxTitleLength, nameof(title));
        Guard.NotNullOrWhiteSpace(description, nameof(description));
        Guard.MaxLength(description, MaxDescriptionLength, nameof(description));

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

    public void Assign(Assignee? assignee)
    {
        if (Status == TicketStatus.Closed)
            throw new DomainException(TicketErrors.CannotAssignClosed());

        Assignee = assignee;
    }

    public Comment AddComment(string content, DateTimeOffset createdAt, string? author = null)
    {
        var comment = new Comment(CommentId.New(), content, createdAt, author);
        _comments.Add(comment);
        return comment;
    }
}
