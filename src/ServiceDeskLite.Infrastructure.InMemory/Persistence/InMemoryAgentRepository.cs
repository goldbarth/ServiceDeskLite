using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Domain.Agents;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

internal sealed class InMemoryAgentRepository : IAgentRepository
{
    private readonly InMemoryStore _store;
    private readonly InMemoryUnitOfWork _unitOfWork;

    public InMemoryAgentRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork)
    {
        _store = store;
        _unitOfWork = unitOfWork;
    }

    public Task AddAsync(Agent agent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _unitOfWork.PendingAdds.Add(agent);
        return Task.CompletedTask;
    }

    public Task<Agent?> GetByIdAsync(AgentId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_store.TryGetAgent(id, out var agent) ? agent : null);
    }

    public Task<IReadOnlyList<Agent>> GetActiveAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<Agent> active = _store.SnapshotAgents()
            .Where(a => a.Active)
            .OrderBy(a => a.Name)
            .ToList();
        return Task.FromResult(active);
    }

    public Task<bool> AnyAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_store.AnyAgents());
    }
}
