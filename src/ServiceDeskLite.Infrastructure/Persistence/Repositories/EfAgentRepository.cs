using Microsoft.EntityFrameworkCore;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Domain.Agents;

namespace ServiceDeskLite.Infrastructure.Persistence.Repositories;

public class EfAgentRepository : IAgentRepository
{
    private readonly ServiceDeskLiteDbContext _dbContext;

    public EfAgentRepository(ServiceDeskLiteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Agent agent, CancellationToken ct = default)
        => _dbContext.Agents.AddAsync(agent, ct).AsTask();

    public Task<Agent?> GetByIdAsync(AgentId id, CancellationToken ct = default)
        => _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Agent>> GetActiveAsync(CancellationToken ct = default)
        => await _dbContext.Agents
            .AsNoTracking()
            .Where(a => a.Active)
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

    public Task<bool> AnyAsync(CancellationToken ct = default)
        => _dbContext.Agents.AnyAsync(ct);
}
