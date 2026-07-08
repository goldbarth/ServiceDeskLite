using System.Globalization;
using System.Text;
using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.SearchTickets;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Lets the model find existing tickets by structured filter (status, priority,
/// assignee) plus optional free text. Executes through SearchTicketsHandler — the
/// same query path as the REST list endpoint — so paging and sort stay consistent.
/// Distinct from find_similar_tickets (semantic dedup before creation): this is a
/// structured filter/list query. Results are compact (no bodies) to limit tokens.
/// </summary>
public sealed partial class SearchTicketsTool
{
    public const string Name = "search_tickets";

    private const int DefaultLimit = 10;
    private const int MaxLimit = 25;

    private readonly SearchTicketsHandler _handler;

    public SearchTicketsTool(SearchTicketsHandler handler)
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
                ["query"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = QueryDescription,
                }),
                ["status"] = JsonSerializer.SerializeToElement(new
                {
                    type = "array",
                    items = new
                    {
                        type = "string",
                        @enum = new[] { "New", "Triaged", "InProgress", "Waiting", "Resolved", "Closed" },
                    },
                    description = StatusDescription,
                }),
                ["priority"] = JsonSerializer.SerializeToElement(new
                {
                    type = "array",
                    items = new
                    {
                        type = "string",
                        @enum = new[] { "Low", "Medium", "High", "Critical" },
                    },
                    description = PriorityDescription,
                }),
                ["assignee"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = AssigneeDescription,
                }),
                ["reference"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = ReferenceDescription,
                }),
                ["sortBy"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    @enum = new[] { "CreatedAt", "DueAt", "Priority", "Status", "Title" },
                    description = SortByDescription,
                }),
                ["sortDirection"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    @enum = new[] { "Asc", "Desc" },
                    description = SortDirectionDescription,
                }),
                ["limit"] = JsonSerializer.SerializeToElement(new
                {
                    type = "integer",
                    minimum = 1,
                    maximum = MaxLimit,
                    description = LimitDescription,
                }),
            },
            Required = [],
        },
    };

    /// <summary>Maps and validates tool input to a SearchTicketsQuery. Static and side-effect free for unit testing.</summary>
    public static bool TryParseInput(JsonElement input, out SearchTicketsQuery? query, out string? error)
    {
        query = null;

        if (input.ValueKind is not JsonValueKind.Object)
        {
            error = "Tool input must be a JSON object.";
            return false;
        }

        string? text = null;
        if (input.TryGetProperty("query", out var queryEl) && queryEl.ValueKind is not JsonValueKind.Null)
        {
            if (queryEl.ValueKind is not JsonValueKind.String)
            {
                error = "Property 'query' must be a string.";
                return false;
            }

            var value = queryEl.GetString();
            text = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        if (!TryParseEnumArray<TicketStatus>(input, "status", out var statuses, out error))
            return false;

        if (!TryParseEnumArray<TicketPriority>(input, "priority", out var priorities, out error))
            return false;

        string? assignee = null;
        if (input.TryGetProperty("assignee", out var assigneeEl) && assigneeEl.ValueKind is not JsonValueKind.Null)
        {
            if (assigneeEl.ValueKind is not JsonValueKind.String)
            {
                error = "Property 'assignee' must be a string.";
                return false;
            }

            var value = assigneeEl.GetString();
            assignee = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        string? reference = null;
        if (input.TryGetProperty("reference", out var referenceEl) && referenceEl.ValueKind is not JsonValueKind.Null)
        {
            if (referenceEl.ValueKind is not JsonValueKind.String)
            {
                error = "Property 'reference' must be a string.";
                return false;
            }

            var value = referenceEl.GetString();
            reference = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        if (!TryParseSort(input, out var sort, out error))
            return false;

        var limit = DefaultLimit;
        if (input.TryGetProperty("limit", out var limitEl) && limitEl.ValueKind is not JsonValueKind.Null)
        {
            if (limitEl.ValueKind is not JsonValueKind.Number
                || !limitEl.TryGetInt32(out var parsedLimit)
                || parsedLimit is < 1 or > MaxLimit)
            {
                error = $"Property 'limit' must be an integer between 1 and {MaxLimit}.";
                return false;
            }

            limit = parsedLimit;
        }

        var criteria = new TicketSearchCriteria(
            Text: text,
            Statuses: statuses,
            Priorities: priorities,
            AssigneeName: assignee,
            Reference: reference);

        query = new SearchTicketsQuery(criteria, new Paging(PagingPolicy.MinPage, limit), sort);
        error = null;
        return true;
    }

    /// <summary>Formats matches for the model: compact, one line per ticket, no bodies.</summary>
    public static string FormatResult(SearchTicketsResult result)
    {
        var page = result.Page;
        if (page.Items.Count == 0)
            return "No tickets matched the given filters.";

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"Found {page.TotalCount} matching ticket(s), showing {page.Items.Count}:");

        foreach (var t in page.Items)
        {
            var assignee = string.IsNullOrWhiteSpace(t.Assignee) ? "unassigned" : t.Assignee;
            sb.AppendLine();
            sb.Append(CultureInfo.InvariantCulture,
                $"- id={t.Id.Value} | \"{t.Title}\" | status={t.Status} | assignee={assignee} | priority={t.Priority}");
        }

        return sb.ToString();
    }

    public async Task<ToolResult> ExecuteAsync(
        JsonElement input,
        CancellationToken ct)
    {
        if (!TryParseInput(input, out var query, out var parseError))
            return ($"Invalid tool input: {parseError}", true, null, null);

        var result = await _handler.HandleAsync(query, ct);

        if (!result.IsSuccess)
        {
            var e = result.Error!;
            return ($"Ticket search failed ({e.Code}): {e.Message}", true, null, null);
        }

        return (FormatResult(result.Value!), false, null, null);
    }

    private static bool TryParseEnumArray<TEnum>(
        JsonElement input,
        string property,
        out IReadOnlyCollection<TEnum>? values,
        out string? error)
        where TEnum : struct, Enum
    {
        values = null;
        error = null;

        if (!input.TryGetProperty(property, out var arrayEl) || arrayEl.ValueKind is JsonValueKind.Null)
            return true;

        if (arrayEl.ValueKind is not JsonValueKind.Array)
        {
            error = $"Property '{property}' must be an array of strings.";
            return false;
        }

        var parsed = new List<TEnum>();
        foreach (var element in arrayEl.EnumerateArray())
        {
            if (element.ValueKind is not JsonValueKind.String
                || !Enum.TryParse<TEnum>(element.GetString(), ignoreCase: true, out var value)
                || !Enum.IsDefined(value))
            {
                error = $"Property '{property}' contains an invalid value. Allowed: {string.Join(", ", Enum.GetNames<TEnum>())}.";
                return false;
            }

            parsed.Add(value);
        }

        values = parsed.Count > 0 ? parsed : null;
        return true;
    }

    private static bool TryParseSort(JsonElement input, out SortSpec? sort, out string? error)
    {
        sort = null;
        error = null;

        var hasField = input.TryGetProperty("sortBy", out var fieldEl) && fieldEl.ValueKind is not JsonValueKind.Null;
        var hasDir = input.TryGetProperty("sortDirection", out var dirEl) && dirEl.ValueKind is not JsonValueKind.Null;

        if (!hasField && !hasDir)
            return true;

        var field = SortSpec.Default.Field;
        if (hasField)
        {
            if (fieldEl.ValueKind is not JsonValueKind.String
                || !Enum.TryParse<TicketSortField>(fieldEl.GetString(), ignoreCase: true, out var parsedField)
                || !Enum.IsDefined(parsedField))
            {
                error = $"Property 'sortBy' must be one of: {string.Join(", ", Enum.GetNames<TicketSortField>())}.";
                return false;
            }

            field = parsedField;
        }

        var direction = SortSpec.Default.Direction;
        if (hasDir)
        {
            if (dirEl.ValueKind is not JsonValueKind.String
                || !Enum.TryParse<SortDirection>(dirEl.GetString(), ignoreCase: true, out var parsedDir)
                || !Enum.IsDefined(parsedDir))
            {
                error = "Property 'sortDirection' must be one of: Asc, Desc.";
                return false;
            }

            direction = parsedDir;
        }

        sort = new SortSpec(field, direction);
        return true;
    }
}
