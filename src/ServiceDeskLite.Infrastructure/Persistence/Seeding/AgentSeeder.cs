using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Agents.Seeding;

namespace ServiceDeskLite.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the fixed fictitious agent roster. Port-based, so the same seeder runs against
/// both persistence providers (registered once in the composition root). Idempotent:
/// seeds only when the roster is empty.
/// </summary>
public sealed class AgentSeeder : IAgentSeeder
{
    private readonly IAgentRepository _agents;
    private readonly IUnitOfWork _uow;

    public AgentSeeder(IAgentRepository agents, IUnitOfWork uow)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _agents.AnyAsync(ct))
            return;

        foreach (var agent in AgentRoster.Seed)
            await _agents.AddAsync(agent, ct);

        await _uow.SaveChangesAsync(ct);
    }
}
