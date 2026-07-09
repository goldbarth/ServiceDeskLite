using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.AddComment;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Lets the agent write on a ticket without changing it: a follow-up question, a proposed
/// solution, or the reasoning behind an action it is asking a human to approve.
/// </summary>
/// <remarks>
/// This is the autonomous worker's only unconditional way to reach a person (ADR-0037). Everything
/// it may not do on its own it says here instead, addressed to whoever owns the ticket, and the
/// comment is attributed to the worker like any other action.
/// </remarks>
public sealed partial class AddCommentTool
{
    public const string Name = "add_comment";

    private readonly AddCommentHandler _handler;
    private readonly IAgentActor _actor;
    private readonly IClock _clock;

    public AddCommentTool(AddCommentHandler handler, IAgentActor actor, IClock clock)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public static Tool Definition => new()
    {
        Name = Name,
        Description = ToolDescription,
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["ticketId"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    format = "uuid",
                    description = TicketIdDescription,
                }),
                ["content"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = ContentDescription,
                }),
            },
            Required = ["ticketId", "content"],
        },
    };

    /// <summary>Maps tool input to an AddCommentCommand. Static for unit testing.</summary>
    /// <param name="actor">Comment author and audit actor; defaults to the interactive assistant.</param>
    public static bool TryParseInput(
        JsonElement input,
        DateTimeOffset createdAt,
        out AddCommentCommand? command,
        out string? error,
        string actor = Domain.Audit.AuditActors.AiAssistant)
    {
        command = null;

        if (input.ValueKind is not JsonValueKind.Object)
        {
            error = "Tool input must be a JSON object.";
            return false;
        }

        if (!input.TryGetProperty("ticketId", out var idEl)
            || idEl.ValueKind is not JsonValueKind.String
            || !Guid.TryParse(idEl.GetString(), out var ticketId))
        {
            error = "Missing or invalid required property 'ticketId' (must be a UUID from a previous "
                    + "create_ticket or search_tickets result).";
            return false;
        }

        if (!input.TryGetProperty("content", out var contentEl)
            || contentEl.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(contentEl.GetString()))
        {
            error = "Missing or invalid required string property 'content'.";
            return false;
        }

        var content = contentEl.GetString()!;
        if (content.Length > Comment.MaxContentLength)
        {
            // Caught here rather than at the handler so the model learns the limit as a number it
            // can act on, instead of a field-error dictionary it has to interpret.
            error = $"Property 'content' must not exceed {Comment.MaxContentLength} characters "
                    + $"(was {content.Length}). Summarise it.";
            return false;
        }

        command = new AddCommentCommand(new TicketId(ticketId), content, actor, createdAt);
        error = null;
        return true;
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct)
    {
        if (!TryParseInput(input, _clock.UtcNow, out var command, out var parseError, _actor.Actor))
            return ($"Invalid tool input: {parseError}", true, null, null);

        var result = await _handler.HandleAsync(command, ct);

        if (!result.IsSuccess)
        {
            var e = result.Error!;
            return ($"Adding the comment failed ({e.Code}): {e.Message}", true, null, null);
        }

        var id = command!.TicketId.Value;
        return ($"Comment added to ticket {id}.", false, id, null);
    }
}
