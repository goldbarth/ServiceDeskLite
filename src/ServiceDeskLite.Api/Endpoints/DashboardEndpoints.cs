using ServiceDeskLite.Api.Http.ProblemDetails;
using ServiceDeskLite.Api.Mapping.Dashboard;
using ServiceDeskLite.Application.Assistant.GetAiDashboard;
using ServiceDeskLite.Application.Tickets.GetDashboardSummary;
using ServiceDeskLite.Contracts.V1.Dashboard;

namespace ServiceDeskLite.Api.Endpoints;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder dashboard)
    {
        // GET /api/v1/dashboard/summary
        dashboard.MapGet("/summary", GetDashboardSummaryAsync)
            .WithName("Dashboard_GetSummary")
            .WithSummary("Get dashboard KPI summary")
            .WithDescription("Returns aggregated KPI counts: active tickets by status, overdue tickets, and tickets resolved in the last 7 days.")
            .Produces<DashboardSummaryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // GET /api/v1/dashboard/ai
        dashboard.MapGet("/ai", GetAiDashboardAsync)
            .WithName("Dashboard_GetAiMetrics")
            .WithSummary("Get AI operations metrics")
            .WithDescription(
                "Returns assistant metrics over a trailing 7-day window: ticket volume, automation rate, "
                + "duplicate-check hit rate, retrieval confidence, per-tool call statistics, and token usage. "
                + "Rates are null rather than zero when no data backs them.")
            .Produces<AiDashboardResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return dashboard;
    }

    private static async Task<IResult> GetAiDashboardAsync(
        HttpContext ctx,
        GetAiDashboardHandler handler,
        ResultToProblemDetailsMapper mapper,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetAiDashboardQuery(), ct);
        return result.ToHttpResult(ctx, mapper, dto => Results.Ok(dto.ToResponse()));
    }

    private static async Task<IResult> GetDashboardSummaryAsync(
        HttpContext ctx,
        GetDashboardSummaryHandler handler,
        ResultToProblemDetailsMapper mapper,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetDashboardSummaryQuery(), ct);
        return result.ToHttpResult(ctx, mapper, dto => Results.Ok(dto.ToResponse()));
    }
}