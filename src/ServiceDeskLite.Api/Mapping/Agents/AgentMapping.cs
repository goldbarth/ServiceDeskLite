using ServiceDeskLite.Application.Agents;
using ServiceDeskLite.Contracts.V1.Agents;

namespace ServiceDeskLite.Api.Mapping.Agents;

internal static class AgentMapping
{
    public static AgentResponse ToResponse(this AgentDto dto)
        => new(dto.Id.Value, dto.Name, dto.Email);
}
