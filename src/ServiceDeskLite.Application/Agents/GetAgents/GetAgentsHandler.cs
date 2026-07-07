using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Application.Agents.GetAgents;

public sealed class GetAgentsHandler
{
    private readonly IAgentRepository _agents;

    public GetAgentsHandler(IAgentRepository agents)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
    }

    public async Task<Result<IReadOnlyList<AgentDto>>> HandleAsync(
        GetAgentsQuery? query,
        CancellationToken ct = default)
    {
        var agents = await _agents.GetActiveAsync(ct);

        var dtos = agents
            .Select(a => new AgentDto(a.Id, a.Name, a.Email))
            .ToList();

        return Result<IReadOnlyList<AgentDto>>.Success(dtos);
    }
}
