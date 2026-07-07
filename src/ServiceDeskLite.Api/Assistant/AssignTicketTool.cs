using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Agents.GetAgents;
using ServiceDeskLite.Application.Tickets.AssignTicket;
using ServiceDeskLite.Domain.Agents;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Lets the model assign, reassign or unassign a ticket. The agent name is resolved
/// against the active roster (GetAgentsHandler); an unknown/inactive name is returned
/// to the model as an error tool_result listing the valid agents, so it can ask the
/// user instead of inventing one. Execution goes through AssignTicketHandler, so the
/// closed-ticket rule and audit (actor "ai-assistant") apply unchanged (ADR-0025).
/// </summary>
public sealed partial class AssignTicketTool
{
    public const string Name = "assign_ticket";
    private const string AssistantActor = "ai-assistant";

    private readonly AssignTicketHandler _handler;
    private readonly GetAgentsHandler _agents;

    public AssignTicketTool(AssignTicketHandler handler, GetAgentsHandler agents)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
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
                ["assignee"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = AssigneeDescription,
                }),
            },
            Required = ["ticketId"],
        },
    };

    /// <summary>Maps tool input. assigneeName is null when omitted/empty (= unassign). Static for unit testing.</summary>
    public static bool TryParseInput(JsonElement input, out Guid ticketId, out string? assigneeName, out string? error)
    {
        ticketId = Guid.Empty;
        assigneeName = null;

        if (input.ValueKind is not JsonValueKind.Object)
        {
            error = "Tool input must be a JSON object.";
            return false;
        }

        if (!input.TryGetProperty("ticketId", out var idEl)
            || idEl.ValueKind is not JsonValueKind.String
            || !Guid.TryParse(idEl.GetString(), out ticketId))
        {
            error = "Missing or invalid required property 'ticketId' (must be a UUID from a previous " +
                    "create_ticket or search_tickets result).";
            return false;
        }

        if (input.TryGetProperty("assignee", out var nameEl) && nameEl.ValueKind is not JsonValueKind.Null)
        {
            if (nameEl.ValueKind is not JsonValueKind.String)
            {
                error = "Property 'assignee' must be a string.";
                return false;
            }

            var value = nameEl.GetString();
            assigneeName = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        error = null;
        return true;
    }

    public async Task<(string Content, bool IsError, Guid? TicketId)> ExecuteAsync(
        JsonElement input,
        CancellationToken ct)
    {
        if (!TryParseInput(input, out var ticketId, out var assigneeName, out var parseError))
            return ($"Invalid tool input: {parseError}", true, null);

        AgentId? agentId = null;
        if (assigneeName is not null)
        {
            var roster = await _agents.HandleAsync(new GetAgentsQuery(), ct);
            var match = roster.Value?
                .FirstOrDefault(a => a.Name.Equals(assigneeName, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                var available = roster.Value is { Count: > 0 }
                    ? string.Join(", ", roster.Value.Select(a => a.Name))
                    : "(none)";
                return ($"No active agent named \"{assigneeName}\". Available agents: {available}.", true, null);
            }

            agentId = match.Id;
        }

        var command = new AssignTicketCommand(new TicketId(ticketId), agentId, AssistantActor);
        var result = await _handler.HandleAsync(command, ct);

        if (!result.IsSuccess)
        {
            var e = result.Error!;
            return ($"Assignment failed ({e.Code}): {e.Message}", true, null);
        }

        var message = assigneeName is null
            ? $"Ticket {ticketId} unassigned."
            : $"Ticket {ticketId} assigned to {assigneeName}.";
        return (message, false, ticketId);
    }
}
