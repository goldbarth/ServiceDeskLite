using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Domain.Audit;
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

        var ticketAdds = PendingAdds.OfType<Ticket>().ToArray();
        if (ticketAdds.Length > 0)
            _store.ApplyAdds(ticketAdds);

        var auditEventAdds = PendingAdds.OfType<AuditEvent>().ToArray();
        if (auditEventAdds.Length > 0)
            _store.AppendAuditEvents(auditEventAdds);

        PendingAdds.Clear();
        return Task.CompletedTask;
    }
}
