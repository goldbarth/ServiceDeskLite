namespace ServiceDeskLite.Contracts.V1.Agents;

public sealed record AgentResponse(Guid Id, string Name, string Email);
