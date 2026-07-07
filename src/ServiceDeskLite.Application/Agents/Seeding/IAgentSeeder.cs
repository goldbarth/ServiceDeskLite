namespace ServiceDeskLite.Application.Agents.Seeding;

public interface IAgentSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
