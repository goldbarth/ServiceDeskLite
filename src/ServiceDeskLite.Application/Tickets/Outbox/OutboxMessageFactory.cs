using System.Text.Json;
using ServiceDeskLite.Domain.Outbox;
using ServiceDeskLite.Domain.Tickets.Events;

namespace ServiceDeskLite.Application.Tickets.Outbox;

/// <summary>
/// Translates domain events into <see cref="OutboxMessage"/> instances.
///
/// Mirrors the structure of <see cref="Audit.AuditEventFactory"/>: the Domain layer
/// raises events, the Application layer serialises them and decides what goes where.
/// JSON serialisation lives here, not in the Domain.
/// </summary>
internal static class OutboxMessageFactory
{
    public static OutboxMessage FromTicketCreated(
        TicketCreatedDomainEvent e, DateTimeOffset occurredAt)
    {
        var payload = JsonSerializer.Serialize(new
        {
            ticketId = e.TicketId.Value,
            title    = e.Title,
            priority = e.Priority.ToString(),
        });

        return new OutboxMessage(
            OutboxMessageId.New(),
            eventType: "ticket.created",
            payload,
            occurredAt);
    }
}