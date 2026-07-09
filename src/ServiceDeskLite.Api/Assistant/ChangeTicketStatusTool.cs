using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Tickets.ChangeTicketStatus;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Lets the model advance a ticket through the workflow (e.g. "mark it in progress",
/// "close it"). Executes through ChangeTicketStatusHandler, so the domain state machine
/// stays authoritative: invalid transitions are rejected and surfaced back to the model
/// as an error tool_result, and the change is audited under the acting agent's actor.
/// </summary>
public sealed partial class ChangeTicketStatusTool
{
    public const string Name = "change_ticket_status";

    private readonly ChangeTicketStatusHandler _handler;
    private readonly IAgentActor _actor;

    public ChangeTicketStatusTool(ChangeTicketStatusHandler handler, IAgentActor actor)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
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
                ["status"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    @enum = new[] { "New", "Triaged", "InProgress", "Waiting", "Resolved", "Closed" },
                    description = StatusDescription,
                }),
            },
            Required = ["ticketId", "status"],
        },
    };

    /// <summary>Maps tool input to a ChangeTicketStatusCommand. Static for unit testing.</summary>
    /// <param name="actor">Audit actor; defaults to the interactive assistant.</param>
    public static bool TryParseInput(
        JsonElement input,
        out ChangeTicketStatusCommand? command,
        out string? error,
        string actor = AuditActors.AiAssistant)
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

        if (!input.TryGetProperty("status", out var statusEl)
            || statusEl.ValueKind is not JsonValueKind.String
            || !Enum.TryParse<TicketStatus>(statusEl.GetString(), ignoreCase: true, out var status)
            || !Enum.IsDefined(status))
        {
            error = "Missing or invalid required property 'status'. Allowed: " +
                    $"{string.Join(", ", Enum.GetNames<TicketStatus>())}.";
            return false;
        }

        command = new ChangeTicketStatusCommand(new TicketId(ticketId), status, actor);
        error = null;
        return true;
    }

    public async Task<ToolResult> ExecuteAsync(
        JsonElement input,
        CancellationToken ct)
    {
        if (!TryParseInput(input, out var command, out var parseError, _actor.Actor))
            return ($"Invalid tool input: {parseError}", true, null, null);

        var result = await _handler.HandleAsync(command, ct);

        if (!result.IsSuccess)
        {
            var e = result.Error!;
            return ($"Status change failed ({e.Code}): {e.Message}", true, null, null);
        }

        var id = command!.Id.Value;
        return ($"Ticket {id} status changed to {command.NewStatus}.", false, id, null);
    }
}
