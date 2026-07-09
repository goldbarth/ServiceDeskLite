using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Tickets.Routing;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Auto-triage a ticket from its content: the deterministic router suggests a category,
/// priority, assignee, and status, and when confident enough the decision is applied
/// through the existing update/assign/change-status handlers (audited, actor
/// "ai-assistant"). Below the confidence threshold the tool returns the suggestion
/// without applying it, so the model can confirm with the user instead of committing an
/// uncertain triage (issue #159, ADR-0032).
/// </summary>
public sealed partial class RouteTicketTool
{
    public const string Name = "route_ticket";

    private readonly RouteTicketHandler _handler;

    public RouteTicketTool(RouteTicketHandler handler)
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
            },
            Required = ["ticketId"],
        },
    };

    public static bool TryParseInput(JsonElement input, out Guid ticketId, out string? error)
    {
        ticketId = Guid.Empty;

        if (input.ValueKind is not JsonValueKind.Object)
        {
            error = "Tool input must be a JSON object.";
            return false;
        }

        if (!input.TryGetProperty("ticketId", out var idEl)
            || idEl.ValueKind is not JsonValueKind.String
            || !Guid.TryParse(idEl.GetString(), out ticketId))
        {
            error = "Missing or invalid required property 'ticketId' (a UUID from a previous "
                + "create_ticket or search_tickets result).";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>Formats the routing outcome for the model. Static and pure for unit testing.</summary>
    public static string FormatResult(Guid ticketId, RouteTicketResult result)
    {
        var d = result.Decision;
        var pct = $"{(int)Math.Round(d.Confidence * 100, MidpointRounding.AwayFromZero)}%";

        if (result.Applied)
            return $"Routed ticket {ticketId} (confidence {pct}): {string.Join("; ", result.AppliedChanges)}. {d.Rationale}";

        // Suggestion only (low confidence or nothing applicable).
        var assignee = d.SuggestedAssignee ?? "none";
        return $"Routing is uncertain for ticket {ticketId} (confidence {pct}) — NOT applied. "
            + $"Suggested: category={d.Category}, priority={d.Priority}, assignee={assignee}, status={d.SuggestedStatus}. "
            + $"{d.Rationale} Confirm with the user before applying, or set the fields explicitly.";
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken ct)
    {
        if (!TryParseInput(input, out var ticketId, out var parseError))
            return new ToolResult($"Invalid tool input: {parseError}", true);

        var result = await _handler.HandleAsync(new RouteTicketCommand(new TicketId(ticketId), AuditActors.AiAssistant), ct);
        if (!result.IsSuccess)
        {
            var e = result.Error!;
            return new ToolResult($"Routing failed ({e.Code}): {e.Message}", true);
        }

        var routing = result.Value!;
        return new ToolResult(
            FormatResult(ticketId, routing),
            IsError: false,
            TicketId: ticketId,
            Confidence: routing.Decision.Confidence);
    }
}
