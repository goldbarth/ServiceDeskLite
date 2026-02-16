using System.Collections.Concurrent;

using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

internal sealed class InMemoryStore
{
    private readonly ConcurrentDictionary<TicketId, Ticket>  _tickets = new();
    
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
}
