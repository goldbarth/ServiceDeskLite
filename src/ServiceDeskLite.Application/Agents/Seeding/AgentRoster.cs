using ServiceDeskLite.Domain.Agents;

namespace ServiceDeskLite.Application.Agents.Seeding;

/// <summary>
/// The fixed set of fictitious agents seeded for the demo. Ids are stable constants so
/// both persistence providers (and tests) seed an identical roster. Purely fictitious —
/// no real accounts, no login.
/// </summary>
public static class AgentRoster
{
    public static IReadOnlyList<Agent> Seed { get; } =
    [
        new(new AgentId(Guid.Parse("019400a0-0001-7000-8000-000000000001")), "Alex Kim", "alex.kim@servicedesk.example"),
        new(new AgentId(Guid.Parse("019400a0-0002-7000-8000-000000000002")), "Sam Rivera", "sam.rivera@servicedesk.example"),
        new(new AgentId(Guid.Parse("019400a0-0003-7000-8000-000000000003")), "Priya Nair", "priya.nair@servicedesk.example"),
        new(new AgentId(Guid.Parse("019400a0-0004-7000-8000-000000000004")), "Jordan Lee", "jordan.lee@servicedesk.example"),
        new(new AgentId(Guid.Parse("019400a0-0005-7000-8000-000000000005")), "Morgan Diaz", "morgan.diaz@servicedesk.example"),
    ];
}
