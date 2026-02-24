using Microsoft.AspNetCore.Mvc;

using ServiceDeskLite.Api.Http.ProblemDetails;
using ServiceDeskLite.Api.Mapping.Tickets;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.ChangeTicketStatus;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Endpoints;

public static class TicketsEndpoints
{
    public static RouteGroupBuilder MapTicketsEndpoints(this RouteGroupBuilder tickets)
    {
        // GET /api/v1/tickets?page=1&pageSize=20
        tickets.MapGet("/", SearchTicketsAsync)
            .WithName("Tickets_Search")
            .WithSummary("Search tickets")
            .WithDescription("Returns a paged list of tickets with deterministic sorting (CreatedAt + Id).")
            .Produces<PagedResponse<TicketListItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        
        // POST /api/v1/tickets
        tickets.MapPost("/", CreateTicketAsync)
            .WithName("Tickets_Create")
            .WithSummary("Create a new ticket")
            .Produces<CreateTicketResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // GET /api/v1/tickets/{id}
        tickets.MapGet("/{id:guid}", GetTicketByIdAsync)
            .WithName("Tickets_GetById")
            .WithSummary("Get ticket by id")
            .Produces<TicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // POST /api/v1/tickets/{id}/status
        tickets.MapPost("/{id:guid}/status", ChangeTicketStatusAsync)
            .WithName("Tickets_ChangeStatus")
            .WithSummary("Change ticket status")
            .Produces<TicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return tickets;
    }

    private static async Task<IResult> CreateTicketAsync(
        HttpContext ctx,
        [FromBody] CreateTicketRequest request,
        CreateTicketHandler handler,
        ResultToProblemDetailsMapper mapper,
        CancellationToken ct)
    {
        var cmd = new CreateTicketCommand(
            Title: request.Title,
            Description: request.Description,
            Priority: request.Priority.ToDomain(),
            CreatedAt: DateTimeOffset.UtcNow,
            DueAt: request.DueAt);

        var result = await handler.HandleAsync(cmd, ct);

        return result.ToHttpResult(ctx, mapper, success =>
        {
            var id = success.Id.Value;
            var body = new CreateTicketResponse(id);
            return Results.Created($"/api/v1/tickets/{id}", body);
        });
    }

    
    private static async Task<IResult> GetTicketByIdAsync(
        HttpContext ctx,
        Guid id,
        GetTicketByIdHandler handler,
        ResultToProblemDetailsMapper mapper,
        CancellationToken ct)
    {
        var query = new GetTicketByIdQuery(new TicketId(id));
        var result = await handler.HandleAsync(query, ct);

        return result.ToHttpResult(ctx, mapper, dto => Results.Ok(dto.ToResponse()));
    }

    private static async Task<IResult> ChangeTicketStatusAsync(
        HttpContext ctx,
        Guid id,
        [FromBody] ChangeTicketStatusRequest request,
        ChangeTicketStatusHandler handler,
        ResultToProblemDetailsMapper mapper,
        CancellationToken ct)
    {
        var cmd = new ChangeTicketStatusCommand(
            Id: new TicketId(id),
            NewStatus: request.NewStatus.ToDomain());

        var result = await handler.HandleAsync(cmd, ct);

        return result.ToHttpResult(ctx, mapper, dto => Results.Ok(dto.ToResponse()));
    }

    private static async Task<IResult> SearchTicketsAsync(
        HttpContext ctx,
        [AsParameters] SearchTicketsRequest request,
        SearchTicketsHandler handler,
        ResultToProblemDetailsMapper mapper,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Page < PagingPolicy.MinPage)
            errors["page"] = [$"must be >= {PagingPolicy.MinPage}"];

        if (request.PageSize is < PagingPolicy.MinPageSize or > PagingPolicy.MaxPageSize)
            errors["pageSize"] = [$"must be between {PagingPolicy.MinPageSize} and {PagingPolicy.MaxPageSize}"];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);
        
        var query = new SearchTicketsQuery(
            Criteria: TicketSearchCriteria.Empty,
            Paging: request.ToPaging(),
            Sort: request.ToSort());

        var result = await handler.HandleAsync(query, ct);

        return result.ToHttpResult(ctx, mapper, success =>
            Results.Ok(success.Page.ToPagedResponse()));
    }
}
