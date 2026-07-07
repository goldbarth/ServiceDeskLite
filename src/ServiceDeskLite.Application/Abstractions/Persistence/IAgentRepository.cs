using ServiceDeskLite.Domain.Agents;

namespace ServiceDeskLite.Application.Abstractions.Persistence;

public interface IAgentRepository
{
    Task AddAsync(Agent agent, CancellationToken ct = default);

    Task<Agent?> GetByIdAsync(AgentId id, CancellationToken ct = default);

    /// <summary>Active agents only, ordered by name — the assignable roster.</summary>
    Task<IReadOnlyList<Agent>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Whether any agent exists — used to keep seeding idempotent.</summary>
    Task<bool> AnyAsync(CancellationToken ct = default);
}
