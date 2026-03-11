using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Domain.Outbox;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

internal sealed class InMemoryOutboxRepository : IOutboxRepository
{
    private readonly InMemoryUnitOfWork _unitOfWork;

    public InMemoryOutboxRepository(InMemoryUnitOfWork unitOfWork)
        => _unitOfWork = unitOfWork;

    public Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _unitOfWork.PendingAdds.Add(message);
        return Task.CompletedTask;
    }
}