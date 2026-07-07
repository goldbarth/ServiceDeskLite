using ServiceDeskLite.Api.Http.ProblemDetails;
using ServiceDeskLite.Api.Mapping.Agents;
using ServiceDeskLite.Application.Agents.GetAgents;
using ServiceDeskLite.Contracts.V1.Agents;

namespace ServiceDeskLite.Api.Endpoints;

public static class AgentsEndpoints
{
    public static RouteGroupBuilder MapAgentsEndpoints(this RouteGroupBuilder agents)
    {
        // GET /api/v1/agents
        agents.MapGet("/", GetAgentsAsync)
            .WithName("Agents_List")
            .WithSummary("List assignable agents")
            .WithDescription("Returns the active agent roster (id, name, email) available for ticket assignment.")
            .Produces<IReadOnlyList<AgentResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return agents;
    }

    private static async Task<IResult> GetAgentsAsync(
        HttpContext ctx,
        GetAgentsHandler handler,
        ResultToProblemDetailsMapper mapper,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetAgentsQuery(), ct);
        return result.ToHttpResult(ctx, mapper,
            agents => Results.Ok(agents.Select(a => a.ToResponse()).ToList()));
    }
}
