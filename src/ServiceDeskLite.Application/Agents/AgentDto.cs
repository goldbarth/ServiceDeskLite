using ServiceDeskLite.Domain.Agents;

namespace ServiceDeskLite.Application.Agents;

public sealed record AgentDto(AgentId Id, string Name, string Email);
