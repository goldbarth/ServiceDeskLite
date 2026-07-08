using Microsoft.AspNetCore.Mvc;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Api.Http.ProblemDetails;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Api.Endpoints;

public static class AssistantEndpoints
{
    public static RouteGroupBuilder MapAssistantEndpoints(this RouteGroupBuilder assistant)
    {
        // POST /api/v1/assistant/chat  (SSE stream)
        assistant.MapPost("/chat", ChatAsync)
            .WithName("Assistant_Chat")
            .WithSummary("Chat with the AI assistant")
            .WithDescription(
                "Streams the model response as Server-Sent Events (text, tool_call, tool_result, citation, error, done). " +
                "The model may create tickets via the create_ticket tool and cite knowledge-base sources.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return assistant;
    }

    private static IResult ChatAsync(
        HttpContext ctx,
        [FromBody] AssistantChatRequest request,
        AssistantChatService service,
        ResultToProblemDetailsMapper mapper)
    {
        if (request.NewMessage is null || string.IsNullOrWhiteSpace(request.NewMessage.Content))
            return mapper.ToProblem(ctx, ApplicationError.Validation(
                "assistant_chat.message.empty",
                "A non-empty user message is required."));

        if (request.NewMessage.Role != AssistantChatRole.User)
            return mapper.ToProblem(ctx, ApplicationError.Validation(
                "assistant_chat.message.not_user",
                "The message must be a user message."));

        return TypedResults.ServerSentEvents(
            service.StreamChatAsync(request.ConversationId, request.NewMessage, ctx.RequestAborted));
    }
}
