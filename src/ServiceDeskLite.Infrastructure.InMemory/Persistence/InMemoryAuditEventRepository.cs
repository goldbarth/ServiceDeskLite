using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

internal sealed class InMemoryAuditEventRepository : IAuditEventRepository
{
    private readonly InMemoryUnitOfWork _unitOfWork;
    private readonly InMemoryStore _store;

    public InMemoryAuditEventRepository(InMemoryUnitOfWork unitOfWork, InMemoryStore store)
    {
        _unitOfWork = unitOfWork;
        _store = store;
    }

    public Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        _unitOfWork.PendingAdds.Add(auditEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default)
        => Task.FromResult(_store.GetAuditEventsByTicketId(ticketId));
}
