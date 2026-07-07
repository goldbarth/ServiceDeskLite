using ServiceDeskLite.Domain.Agents;
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

    // FK to the assigned agent (ADR-0025). null = unassigned. The display name is
    // resolved from the Agents roster; the name at change time is snapshotted into
    // the audit trail via AssigneeChangedDomainEvent, not stored on the ticket.
    public AgentId? AssignedAgentId { get; private set; }

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

    /// <summary>
    /// Assigns (or, with a null agent, unassigns) the ticket. Existence/active
    /// validation of the agent is the handler's responsibility; the names are passed
    /// in only to snapshot a readable, stable record into the audit trail (ADR-0025).
    /// </summary>
    public void Assign(AgentId? agentId, string? assigneeName, string? previousAssigneeName)
    {
        if (Status == TicketStatus.Closed)
            throw new DomainException(TicketErrors.CannotAssignClosed());

        AssignedAgentId = agentId;

        _domainEvents.Add(new AssigneeChangedDomainEvent(Id, previousAssigneeName, assigneeName));
    }

    /// <summary>
    /// Partially updates editable fields; null = keep current value. Raises a single
    /// event carrying only the fields that actually changed; no event on a no-op.
    /// </summary>
    public void UpdateDetails(
        string? title = null,
        string? description = null,
        TicketPriority? priority = null,
        DateTimeOffset? dueAt = null)
    {
        if (Status == TicketStatus.Closed)
            throw new DomainException(TicketErrors.CannotUpdateClosed());

        if (title is not null)
        {
            Guard.NotNullOrWhiteSpace(title, nameof(title));
            Guard.MaxLength(title, MaxTitleLength, nameof(title));
        }

        if (description is not null)
        {
            Guard.NotNullOrWhiteSpace(description, nameof(description));
            Guard.MaxLength(description, MaxDescriptionLength, nameof(description));
        }

        var changedTitle = title is not null && title != Title ? title : null;
        var changedDescription = description is not null && description != Description ? description : null;
        TicketPriority? changedPriority = priority is not null && priority != Priority ? priority : null;
        DateTimeOffset? changedDueAt = dueAt is not null && dueAt != DueAt ? dueAt : null;

        if (changedTitle is null && changedDescription is null && changedPriority is null && changedDueAt is null)
            return;

        Title = changedTitle ?? Title;
        Description = changedDescription ?? Description;
        Priority = changedPriority ?? Priority;
        DueAt = changedDueAt ?? DueAt;

        _domainEvents.Add(new TicketDetailsUpdatedDomainEvent(
            Id, changedTitle, changedDescription, changedPriority, changedDueAt));
    }

    public Comment AddComment(string content, DateTimeOffset createdAt, string? author = null)
    {
        var comment = new Comment(CommentId.New(), content, createdAt, author);
        _comments.Add(comment);

        _domainEvents.Add(new CommentAddedDomainEvent(Id, comment.Author, comment.Content));
        return comment;
    }
}
