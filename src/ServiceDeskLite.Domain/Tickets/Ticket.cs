using ServiceDeskLite.Domain.Common;
using ServiceDeskLite.Domain.Tickets.Events;

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
    
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    // Private constructor for EF Core materialization.
    // EF Core calls this when loading from the database and then sets properties
    // via reflection. Domain events must NOT be raised here — the entity already
    // exists, nothing "happened" from the domain's perspective.
#pragma warning disable CS8618
    private Ticket() { }
#pragma warning restore CS8618

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

        _domainEvents.Add(new TicketCreatedDomainEvent(Id, Title, Priority));
    }

    public void ChangeStatus(TicketStatus newStatus)
    {
        var previousStatus = Status;
        TicketWorkflow.EnsureCanTransition(Status, newStatus);
        Status = newStatus;

        _domainEvents.Add(new StatusChangedDomainEvent(Id, previousStatus, newStatus));
    }

    public void Assign(Assignee? assignee)
    {
        if (Status == TicketStatus.Closed)
            throw new DomainException(TicketErrors.CannotAssignClosed());

        var previousAssignee = Assignee;
        Assignee = assignee;

        _domainEvents.Add(new AssigneeChangedDomainEvent(Id, previousAssignee?.Name, assignee?.Name));
    }

    public Comment AddComment(string content, DateTimeOffset createdAt, string? author = null)
    {
        var comment = new Comment(CommentId.New(), content, createdAt, author);
        _comments.Add(comment);

        _domainEvents.Add(new CommentAddedDomainEvent(Id, comment.Id, author, content.Length));
        return comment;
    }
}
