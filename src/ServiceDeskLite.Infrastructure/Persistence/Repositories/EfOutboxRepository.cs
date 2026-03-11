using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Domain.Outbox;

namespace ServiceDeskLite.Infrastructure.Persistence.Repositories;

public class EfOutboxRepository : IOutboxRepository
{
    private readonly ServiceDeskLiteDbContext _dbContext;

    public EfOutboxRepository(ServiceDeskLiteDbContext dbContext)
        => _dbContext = dbContext;

    public Task AddAsync(OutboxMessage message, CancellationToken ct = default)
        => _dbContext.OutboxMessages.AddAsync(message, ct).AsTask();
}