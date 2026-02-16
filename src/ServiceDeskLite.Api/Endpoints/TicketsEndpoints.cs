using Microsoft.AspNetCore.Mvc;

using ServiceDeskLite.Api.Contracts.Tickets;
using ServiceDeskLite.Api.Mapping;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Endpoints;

public static class TicketsEndpoints
{
    public static RouteGroupBuilder MapTicketsEndpoints(this RouteGroupBuilder tickets)
    {
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

        // GET /api/v1/tickets?page=1&pageSize=20
        tickets.MapGet("/", SearchTicketsAsync)
            .WithName("Tickets_Search")
            .WithSummary("Search tickets")
            .WithDescription("Returns a paged list of tickets with deterministic sorting (CreatedAt + Id).")
            .Produces<PagedResponse<TicketListItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return tickets;
    }

    private static async Task<IResult> CreateTicketAsync(
        [FromBody] CreateTicketRequest request,
        CreateTicketHandler handler,
        CancellationToken ct)
    {
        var cmd = new CreateTicketCommand(
            Title: request.Title,
            Description: request.Description,
            Priority: request.Priority,
            CreatedAt: DateTimeOffset.UtcNow,
            DueAt: request.DueAt);
        
        var result = await handler.HandleAsync(cmd, ct);

        return result.ToHttpResult(success =>
        {
            var id = success.Id.Value;
            var body = new CreateTicketResponse(id);

            return Results.Created($"/api/v1/tickets/{id}", body);
        });
    }
    
    private static async Task<IResult> GetTicketByIdAsync(
        Guid id,
        GetTicketByIdHandler handler,
        CancellationToken ct)
    {
        var query = new GetTicketByIdQuery(new TicketId(id));
        var result = await handler.HandleAsync(query, ct);
        
        return result.ToHttpResult(dto => Results.Ok(dto.ToResponse()));
    }

    private static async Task<IResult> SearchTicketsAsync(
        [AsParameters] SearchTicketsRequest request,
        SearchTicketsHandler handler,
        CancellationToken ct)
    {
        Paging? paging = null;
        if (request.Page != Paging.Default.Page || request.PageSize != Paging.Default.PageSize)
            paging = new Paging(request.Page, request.PageSize);

        SortSpec? sort = null;
        if (request.Sort is not null || request.Direction is not null)
            sort = new SortSpec(
                Field: request.Sort ?? SortSpec.Default.Field,
                Direction: request.Direction ?? SortSpec.Default.Direction);

        var query = new SearchTicketsQuery(
            Criteria: null,  // v1 minimal; später ggf. Filter-DTO → TicketSearchCriteria
            Paging: paging,
            Sort: sort);

        var result = await handler.HandleAsync(query, ct);
        return result.ToHttpResult(success =>
        {
            var page = success.Page;

            var items = page.Items
                .Select(t => t.ToListItemResponse())
                .ToList();
            
            var response = new PagedResponse<TicketListItemResponse>(
                Items: items,
                Page: page.Paging.Page,
                PageSize: page.Paging.PageSize,
                TotalCount: page.TotalCount);
            
            return Results.Ok(response);
        });
    }
}
