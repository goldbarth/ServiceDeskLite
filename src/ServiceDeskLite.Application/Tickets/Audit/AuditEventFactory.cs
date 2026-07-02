using System.Text.Json;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets.Events;

namespace ServiceDeskLite.Application.Tickets.Audit;

/// <summary>
/// Translates domain events into <see cref="AuditEvent"/> instances.
/// This is the single place responsible for JSON payload serialization.
/// The Domain layer has no knowledge of JSON — it only records what happened.
/// </summary>
internal static class AuditEventFactory
{
    public static AuditEvent FromTicketCreated(
        TicketCreatedDomainEvent e, string? actor, DateTimeOffset occurredAt)
    {
        var payload = JsonSerializer.Serialize(new
        {
            title = e.Title,
            priority = e.Priority.ToString()
        });

        return new AuditEvent(
            AuditEventId.New(), e.TicketId,
            AuditEventTypes.TicketCreated, actor, occurredAt, payload);
    }

    public static AuditEvent FromStatusChanged(
        StatusChangedDomainEvent e, string? actor, DateTimeOffset occurredAt)
    {
        var payload = JsonSerializer.Serialize(new
        {
            fromStatus = e.FromStatus.ToString(),
            toStatus = e.ToStatus.ToString()
        });

        return new AuditEvent(
            AuditEventId.New(), e.TicketId,
            AuditEventTypes.StatusChanged, actor, occurredAt, payload);
    }

    public static AuditEvent FromAssigneeChanged(
        AssigneeChangedDomainEvent e, string? actor, DateTimeOffset occurredAt)
    {
        var payload = JsonSerializer.Serialize(new
        {
            previousAssignee = e.PreviousAssignee,
            newAssignee = e.NewAssignee
        });

        return new AuditEvent(
            AuditEventId.New(), e.TicketId,
            AuditEventTypes.AssigneeChanged, actor, occurredAt, payload);
    }

    public static AuditEvent FromDetailsUpdated(
        TicketDetailsUpdatedDomainEvent e, string? actor, DateTimeOffset occurredAt)
    {
        // Only fields that actually changed are present on the event (null = unchanged).
        var payload = JsonSerializer.Serialize(new
        {
            newTitle = e.NewTitle,
            newDescription = e.NewDescription,
            newPriority = e.NewPriority?.ToString(),
            newDueAt = e.NewDueAt
        });

        return new AuditEvent(
            AuditEventId.New(), e.TicketId,
            AuditEventTypes.DetailsUpdated, actor, occurredAt, payload);
    }

    public static AuditEvent FromCommentAdded(
        CommentAddedDomainEvent e, DateTimeOffset occurredAt)
    {
        var actor = string.IsNullOrWhiteSpace(e.Author) ? null : e.Author;
        var payload = JsonSerializer.Serialize(new
        {
            author = actor,
            content = e.Content
        });

        // For comments, the author IS the actor — no separate actor parameter.
        return new AuditEvent(
            AuditEventId.New(), e.TicketId,
            AuditEventTypes.CommentAdded, actor, occurredAt, payload);
    }
}
