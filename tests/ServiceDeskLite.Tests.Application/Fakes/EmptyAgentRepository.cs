using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Domain.Agents;

namespace ServiceDeskLite.Tests.Application.Fakes;

/// <summary>Agent repository with an empty roster — for handlers that don't assign.</summary>
internal sealed class EmptyAgentRepository : IAgentRepository
{
    public Task AddAsync(Agent agent, CancellationToken ct = default) => Task.CompletedTask;

    public Task<Agent?> GetByIdAsync(AgentId id, CancellationToken ct = default)
        => Task.FromResult<Agent?>(null);

    public Task<IReadOnlyList<Agent>> GetActiveAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Agent>>([]);

    public Task<bool> AnyAsync(CancellationToken ct = default) => Task.FromResult(false);
}
