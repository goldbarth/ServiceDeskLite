using Microsoft.EntityFrameworkCore;
using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.Persistence.Repositories;

public class EfAuditEventRepository : IAuditEventRepository
{
    private readonly ServiceDeskLiteDbContext _dbContext;

    public EfAuditEventRepository(ServiceDeskLiteDbContext dbContext)
        => _dbContext = dbContext;

    public Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
        => _dbContext.AuditEvents.AddAsync(auditEvent, ct).AsTask();

    public async Task<IReadOnlyList<AuditEvent>> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default)
        => await _dbContext.AuditEvents
            .AsNoTracking()
            .Where(e => e.TicketId == ticketId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(ct);
}
