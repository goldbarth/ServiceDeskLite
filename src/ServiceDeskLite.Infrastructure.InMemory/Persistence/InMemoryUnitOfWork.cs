using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

internal sealed class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly InMemoryStore _store;

    internal List<object> PendingAdds { get; } = [];

    public InMemoryUnitOfWork(InMemoryStore store)
        => _store = store;
    
    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        
        var ticketsAdds = PendingAdds
            .OfType<Ticket>()
            .ToArray();
        
        if (ticketsAdds.Length > 0)
            _store.ApplyAdds(ticketsAdds);

        var written = ticketsAdds.Length;
        
        PendingAdds.Clear();
        return Task.FromResult(written);
    }
}
