using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.CreateTicket;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// The single tool exposed to the model. Executes through the existing
/// CreateTicketHandler so validation, audit trail and outbox all apply —
/// the model only supplies arguments, it never bypasses the application layer.
/// </summary>
public sealed partial class CreateTicketTool
{
    public const string Name = "create_ticket";
    private const string AssistantActor = "ai-assistant";

    private readonly CreateTicketHandler _handler;
    private readonly IClock _clock;

    public CreateTicketTool(CreateTicketHandler handler, IClock clock)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
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
                ["title"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = TitleDescription,
                }),
                ["description"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = DescriptionDescription,
                }),
                ["priority"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    @enum = new[] { "Low", "Medium", "High", "Critical" },
                    description = PriorityDescription,
                }),
                ["dueAt"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    format = "date-time",
                    description = DueAtDescription,
                }),
            },
            Required = ["title", "description", "priority"],
        },
    };

    /// <summary>
    /// Maps validated tool input to a CreateTicketCommand. Kept static and side-effect
    /// free so it is unit-testable without an API key or handler.
    /// </summary>
    public static bool TryParseInput(
        JsonElement input,
        DateTimeOffset createdAt,
        out CreateTicketCommand? command,
        out string? error)
    {
        command = null;

        if (input.ValueKind is not JsonValueKind.Object)
        {
            error = "Tool input must be a JSON object.";
            return false;
        }

        if (!input.TryGetProperty("title", out var titleEl) || titleEl.ValueKind is not JsonValueKind.String)
        {
            error = "Missing or invalid required string property 'title'.";
            return false;
        }

        if (!input.TryGetProperty("description", out var descEl) || descEl.ValueKind is not JsonValueKind.String)
        {
            error = "Missing or invalid required string property 'description'.";
            return false;
        }

        if (!input.TryGetProperty("priority", out var prioEl)
            || prioEl.ValueKind is not JsonValueKind.String
            || !Enum.TryParse<TicketPriority>(prioEl.GetString(), ignoreCase: true, out var priority))
        {
            error = "Property 'priority' must be one of: Low, Medium, High, Critical.";
            return false;
        }

        DateTimeOffset? dueAt = null;
        if (input.TryGetProperty("dueAt", out var dueEl) && dueEl.ValueKind is not JsonValueKind.Null)
        {
            if (dueEl.ValueKind is not JsonValueKind.String || !dueEl.TryGetDateTimeOffset(out var parsedDue))
            {
                error = "Property 'dueAt' must be an ISO 8601 date-time string.";
                return false;
            }

            // Guard against the model resolving relative dates wrong; the error is fed
            // back as tool_result so it can retry with a corrected date.
            if (parsedDue < createdAt)
            {
                error = $"Property 'dueAt' ({parsedDue:yyyy-MM-dd}) is in the past. " +
                        $"The current date is {createdAt:yyyy-MM-dd}; due dates must be in the future.";
                return false;
            }

            dueAt = parsedDue;
        }

        command = new CreateTicketCommand(
            Title: titleEl.GetString()!,
            Description: descEl.GetString()!,
            Priority: priority,
            CreatedAt: createdAt,
            DueAt: dueAt,
            Actor: AssistantActor);

        error = null;
        return true;
    }

    /// <summary>Executes the tool. Failures come back as (content, isError=true) so the model can self-correct.</summary>
    public async Task<(string Content, bool IsError, Guid? TicketId)> ExecuteAsync(
        JsonElement input,
        CancellationToken ct)
    {
        if (!TryParseInput(input, _clock.UtcNow, out var command, out var parseError))
            return ($"Invalid tool input: {parseError}", true, null);

        var result = await _handler.HandleAsync(command, ct);

        if (!result.IsSuccess)
        {
            var e = result.Error!;
            var fields = e.FieldErrors is { Count: > 0 }
                ? " Field errors: " + string.Join("; ", e.FieldErrors.Select(f => $"{f.Key}: {string.Join(", ", f.Value)}"))
                : string.Empty;
            return ($"Ticket creation failed ({e.Code}): {e.Message}{fields}", true, null);
        }

        var id = result.Value!.Id.Value;
        return ($"Ticket created successfully with id {id}.", false, id);
    }
}
