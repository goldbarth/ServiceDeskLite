namespace ServiceDeskLite.Domain.Agents;

public readonly record struct AgentId(Guid Value)
{
    public static AgentId New() => new(Guid.CreateVersion7());
}
