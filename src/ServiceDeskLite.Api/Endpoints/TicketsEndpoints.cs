using Microsoft.AspNetCore.Mvc;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Api.Http.ProblemDetails;
using ServiceDeskLite.Api.Mapping.Tickets;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Common.Validation;
using ServiceDeskLite.Application.Tickets.AddComment;
using ServiceDeskLite.Application.Tickets.AssignTicket;
using ServiceDeskLite.Application.Tickets.ChangeTicketStatus;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Application.Tickets.GetAuditEvents;
using ServiceDeskLite.Application.Tickets.GetTicketById;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Application.Tickets.UpdateTicket;
using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Domain.Agents;
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

        // GET /api/v1/tickets/{id}/summary  (SSE stream)
        tickets.MapGet("/{id:guid}/summary", StreamTicketSummaryAsync)
            .WithName("Tickets_Summary")
            .WithSummary("Stream an AI summary of a ticket")
            .WithDescription(
                "Streams a structured summary as Server-Sent Events (delta, error, done). Each delta " +
                "carries the section it belongs to: Summary, NextSteps, Risks or MissingInfo.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
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

        // POST /api/v1/tickets/{id}/comments
        tickets.MapPost("/{id:guid}/comments", AddCommentAsync)
            .WithName("Tickets_AddComment")
            .WithSummary("Add a comment to a ticket")
            .Produces<CommentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // PATCH /api/v1/tickets/{id}
        tickets.MapPatch("/{id:guid}", UpdateTicketAsync)
            .WithName("Tickets_Update")
            .WithSummary("Update ticket details")
            .WithDescription("Partial update of title, description, priority, or due date. Omitted fields stay unchanged.")
            .Produces<TicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // GET /api/v1/tickets/{id}/audit-events
        tickets.MapGet("/{id:guid}/audit-events", GetAuditEventsAsync)
            .WithName("Tickets_GetAuditEvents")
            .WithSummary("Get audit history for a ticket")
            .WithDescription("Returns all audit events for a ticket in chronological order.")
            .Produces<IReadOnlyList<AuditEventResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // POST /api/v1/tickets/{id}/assign
        tickets.MapPost("/{id:guid}/assign", AssignTicketAsync)
            .WithName("Tickets_Assign")
            .WithSummary("Assign or unassign a ticket")
            .WithDescription("Set AssigneeName to null to unassign.")
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
        IClock clock,
        CancellationToken ct)
    {
        var cmd = new CreateTicketCommand(
            Title: request.Title,
            Description: request.Description,
            Priority: request.Priority.ToDomain(),
            CreatedAt: clock.UtcNow,
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

    /// <summary>
    /// Resolves the ticket before opening the stream: a missing ticket must surface as a
    /// 404 ProblemDetails, not as a 200 stream whose first event happens to be an error.
    /// </summary>
    private static async Task<IResult> StreamTicketSummaryAsync(
        HttpContext ctx,
        Guid id,
        GetTicketByIdHandler handler,
        TicketSummaryService summaries,
        ResultToProblemDetailsMapper mapper,
        CancellationToken ct)
    {
        var query = new GetTicketByIdQuery(new TicketId(id));
        var result = await handler.HandleAsync(query, ct);

        return result.ToHttpResult(ctx, mapper, ticket =>
            TypedResults.ServerSentEvents(summaries.StreamSummaryAsync(ticket, ct)));
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

    private static async Task<IResult> AddCommentAsync(
        HttpContext ctx,
        Guid id,
        [FromBody] AddCommentRequest request,
        AddCommentHandler handler,
        ResultToProblemDetailsMapper mapper,
        IClock clock,
        CancellationToken ct)
    {
        var cmd = new AddCommentCommand(
            TicketId: new TicketId(id),
            Content: request.Content,
            Author: request.Author,
            CreatedAt: clock.UtcNow);

        var result = await handler.HandleAsync(cmd, ct);

        return result.ToHttpResult(ctx, mapper, success =>
        {
            var c = success.Comment;
            var body = new CommentResponse(c.Id.Value, c.Content, c.Author, c.CreatedAt);
            return Results.Created($"/api/v1/tickets/{id}/comments/{c.Id.Value}", body);
        });
    }

    private static async Task<IResult> UpdateTicketAsync(
        HttpContext ctx,
        Guid id,
        [FromBody] UpdateTicketRequest request,
        UpdateTicketHandler handler,
        ResultToProblemDetailsMapper mapper,
        CancellationToken ct)
    {
        var cmd = new UpdateTicketCommand(
            Id: new TicketId(id),
            Title: request.Title,
            Description: request.Description,
            Priority: request.Priority?.ToDomain(),
            DueAt: request.DueAt,
            Category: request.Category?.ToDomain());

        var result = await handler.HandleAsync(cmd, ct);

        return result.ToHttpResult(ctx, mapper, dto => Results.Ok(dto.ToResponse()));
    }

    private static async Task<IResult> AssignTicketAsync(
        HttpContext ctx,
        Guid id,
        [FromBody] AssignTicketRequest request,
        AssignTicketHandler handler,
        ResultToProblemDetailsMapper mapper,
        CancellationToken ct)
    {
        var cmd = new AssignTicketCommand(
            Id: new TicketId(id),
            AgentId: request.AgentId is { } agentId ? new AgentId(agentId) : null);

        var result = await handler.HandleAsync(cmd, ct);

        return result.ToHttpResult(ctx, mapper, dto => Results.Ok(dto.ToResponse()));
    }

    private static async Task<IResult> GetAuditEventsAsync(
        HttpContext ctx,
        Guid id,
        GetAuditEventsHandler handler,
        ResultToProblemDetailsMapper mapper,
        CancellationToken ct)
    {
        var query = new GetAuditEventsQuery(new TicketId(id));
        var result = await handler.HandleAsync(query, ct);

        return result.ToHttpResult(ctx, mapper, dtos =>
            Results.Ok(dtos.Select(d => d.ToResponse()).ToList()));
    }

    private static async Task<IResult> SearchTicketsAsync(
        HttpContext ctx,
        [AsParameters] SearchTicketsRequest request,
        SearchTicketsHandler handler,
        ResultToProblemDetailsMapper mapper,
        CancellationToken ct)
    {
        var builder = new FieldValidationBuilder();

        if (request.Page < PagingPolicy.MinPage)
            builder.AddError("page", $"must be >= {PagingPolicy.MinPage}");

        if (request.PageSize is < PagingPolicy.MinPageSize or > PagingPolicy.MaxPageSize)
            builder.AddError("pageSize", $"must be between {PagingPolicy.MinPageSize} and {PagingPolicy.MaxPageSize}");

        var validation = builder.Build();
        if (!validation.IsValid)
            return mapper.ToProblem(ctx, ApplicationError.ValidationWithFields(
                "search_tickets.validation_failed",
                "Validation failed.",
                validation.FieldErrors));
        
        var query = new SearchTicketsQuery(
            Criteria: request.ToCriteria(),
            Paging: request.ToPaging(),
            Sort: request.ToSort());

        var result = await handler.HandleAsync(query, ct);

        return result.ToHttpResult(ctx, mapper, success =>
            Results.Ok(success.Page.ToPagedResponse()));
    }
}
