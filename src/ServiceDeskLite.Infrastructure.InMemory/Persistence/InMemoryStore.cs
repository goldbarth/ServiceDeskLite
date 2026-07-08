using System.Collections.Concurrent;

using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Domain.Agents;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Outbox;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

internal sealed class InMemoryStore
{
    private readonly ConcurrentDictionary<TicketId, Ticket> _tickets = new();
    private readonly ConcurrentDictionary<AgentId, Agent> _agents = new();
    private readonly ConcurrentBag<AuditEvent> _auditEvents = new();
    private readonly ConcurrentBag<OutboxMessage> _outboxMessages = new();
    private readonly ConcurrentDictionary<ConversationId, ConversationLog> _conversations = new();

    public bool TryGetAgent(AgentId id, out Agent? agent)
        => _agents.TryGetValue(id, out agent);

    public bool AnyAgents() => !_agents.IsEmpty;

    public IReadOnlyCollection<Agent> SnapshotAgents()
        => _agents.Values.ToArray();

    public void ApplyAgentAdds(IEnumerable<Agent> adds)
    {
        foreach (var agent in adds)
        {
            if (!_agents.TryAdd(agent.Id, agent))
                throw new InvalidOperationException($"Agent already exists: {agent.Id}");
        }
    }

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

    public IReadOnlyList<ConversationMessage> GetConversation(ConversationId id, OwnerId owner)
    {
        if (!_conversations.TryGetValue(id, out var log) || log.Owner != owner)
            return [];

        lock (log)
            return log.Messages.OrderBy(m => m.Sequence).ToList();
    }

    public void AppendConversation(ConversationId id, OwnerId owner, IReadOnlyList<ConversationMessage> messages)
    {
        var log = _conversations.GetOrAdd(id, _ => new ConversationLog(owner));

        // First writer establishes ownership; a mismatched owner is ignored, mirroring
        // the EF store's owner-scoped query (no cross-owner writes).
        if (log.Owner != owner)
            return;

        lock (log)
            log.Messages.AddRange(messages);
    }

    private sealed class ConversationLog(OwnerId owner)
    {
        public OwnerId Owner { get; } = owner;
        public List<ConversationMessage> Messages { get; } = [];
    }
}
