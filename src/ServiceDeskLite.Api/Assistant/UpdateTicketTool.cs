using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Tickets.UpdateTicket;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Lets the model apply a partial update to any existing ticket — one it created
/// earlier in the conversation, or one resolved from the user's description via
/// search_tickets (find-then-update). Executes through UpdateTicketHandler, so
/// domain rules and audit trail (actor "ai-assistant") apply unchanged.
/// </summary>
public sealed partial class UpdateTicketTool
{
    public const string Name = "update_ticket";
    private const string AssistantActor = "ai-assistant";

    private readonly UpdateTicketHandler _handler;

    public UpdateTicketTool(UpdateTicketHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
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
            Required = ["ticketId"],
        },
    };

    /// <summary>Maps tool input to an UpdateTicketCommand. Static for unit testing.</summary>
    public static bool TryParseInput(
        JsonElement input,
        DateTimeOffset now,
        out UpdateTicketCommand? command,
        out string? error)
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
            error = "Missing or invalid required property 'ticketId' (must be a UUID from a previous " +
                    "create_ticket or search_tickets result).";
            return false;
        }

        string? title = null;
        if (input.TryGetProperty("title", out var titleEl) && titleEl.ValueKind is not JsonValueKind.Null)
        {
            if (titleEl.ValueKind is not JsonValueKind.String)
            {
                error = "Property 'title' must be a string.";
                return false;
            }

            title = titleEl.GetString();
        }

        string? description = null;
        if (input.TryGetProperty("description", out var descEl) && descEl.ValueKind is not JsonValueKind.Null)
        {
            if (descEl.ValueKind is not JsonValueKind.String)
            {
                error = "Property 'description' must be a string.";
                return false;
            }

            description = descEl.GetString();
        }

        TicketPriority? priority = null;
        if (input.TryGetProperty("priority", out var prioEl) && prioEl.ValueKind is not JsonValueKind.Null)
        {
            if (prioEl.ValueKind is not JsonValueKind.String
                || !Enum.TryParse<TicketPriority>(prioEl.GetString(), ignoreCase: true, out var parsedPriority))
            {
                error = "Property 'priority' must be one of: Low, Medium, High, Critical.";
                return false;
            }

            priority = parsedPriority;
        }

        DateTimeOffset? dueAt = null;
        if (input.TryGetProperty("dueAt", out var dueEl) && dueEl.ValueKind is not JsonValueKind.Null)
        {
            if (dueEl.ValueKind is not JsonValueKind.String || !dueEl.TryGetDateTimeOffset(out var parsedDue))
            {
                error = "Property 'dueAt' must be an ISO 8601 date-time string.";
                return false;
            }

            if (parsedDue < now)
            {
                error = $"Property 'dueAt' ({parsedDue:yyyy-MM-dd HH:mm zzz}) is in the past. " +
                        $"The current date is {now:yyyy-MM-dd}; due dates must be in the future.";
                return false;
            }

            dueAt = parsedDue;
        }

        if (title is null && description is null && priority is null && dueAt is null)
        {
            error = "At least one of title, description, priority, or dueAt must be provided.";
            return false;
        }

        command = new UpdateTicketCommand(
            Id: new TicketId(ticketId),
            Title: title,
            Description: description,
            Priority: priority,
            DueAt: dueAt,
            Actor: AssistantActor);

        error = null;
        return true;
    }

    public async Task<(string Content, bool IsError, Guid? TicketId, double? Confidence)> ExecuteAsync(
        JsonElement input,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (!TryParseInput(input, now, out var command, out var parseError))
            return ($"Invalid tool input: {parseError}", true, null, null);

        var result = await _handler.HandleAsync(command, ct);

        if (!result.IsSuccess)
        {
            var e = result.Error!;
            var fields = e.FieldErrors is { Count: > 0 }
                ? " Field errors: " + string.Join("; ", e.FieldErrors.Select(f => $"{f.Key}: {string.Join(", ", f.Value)}"))
                : string.Empty;
            return ($"Ticket update failed ({e.Code}): {e.Message}{fields}", true, null, null);
        }

        var id = command!.Id.Value;
        return ($"Ticket {id} updated successfully.", false, id, null);
    }
}
