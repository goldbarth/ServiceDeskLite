using System.Collections.Concurrent;

using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Outbox;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

internal sealed class InMemoryStore
{
    private readonly ConcurrentDictionary<TicketId, Ticket> _tickets = new();
    private readonly ConcurrentBag<AuditEvent> _auditEvents = new();
    private readonly ConcurrentBag<OutboxMessage> _outboxMessages = new();

    public bool TryGetTicket(TicketId id, out Ticket? ticket)
        => _tickets.TryGetValue(id, out ticket);

    public bool ContainsTicket(TicketId id)
        => _tickets.ContainsKey(id);

    public IReadOnlyCollection<Ticket> SnapshotTickets()
        => _tickets.Values.ToArray();

    public void ApplyAdds(IEnumerable<Ticket> adds)
    {
        foreach (var ticket in adds)
        {
            if (!_tickets.TryAdd(ticket.Id, ticket))
                throw new InvalidOperationException($"Ticket already exists: {ticket.Id}");
        }
    }

    public void AppendAuditEvents(IEnumerable<AuditEvent> events)
    {
        foreach (var e in events)
            _auditEvents.Add(e);
    }

    public IReadOnlyList<AuditEvent> GetAuditEventsByTicketId(TicketId ticketId)
        => _auditEvents
            .Where(e => e.TicketId == ticketId)
            .OrderBy(e => e.OccurredAt)
            .ToList();

    public IReadOnlyCollection<AuditEvent> SnapshotAuditEvents()
        => _auditEvents.ToArray();

    public void AppendOutboxMessages(IEnumerable<OutboxMessage> messages)
    {
        foreach (var m in messages)
            _outboxMessages.Add(m);
    }

    public IReadOnlyCollection<OutboxMessage> SnapshotOutboxMessages()
        => _outboxMessages.ToArray();
}
