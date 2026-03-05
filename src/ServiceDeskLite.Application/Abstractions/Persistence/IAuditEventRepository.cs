using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Abstractions.Persistence;

public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default);

    Task<IReadOnlyList<AuditEvent>> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default);
}
