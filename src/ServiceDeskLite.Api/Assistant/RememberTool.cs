using System.Text.Json;

using Anthropic.Models.Messages;

using ServiceDeskLite.Application.Abstractions.Assistant;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Long-term memory write exposed as a tool: the model calls this to persist a
/// durable fact, preference or profile note about the user so a later, separate
/// conversation can recall it via <see cref="RecallMemoryTool"/>. Guarded like
/// every other tool; on deployments without an embedding store it reports memory
/// as unavailable instead of silently dropping the fact.
/// </summary>
public sealed partial class RememberTool
{
    public const string Name = "remember";

    private const string DefaultKind = "fact";
    private static readonly string[] AllowedKinds = ["profile", "preference", "fact"];
    private const int MaxContentLength = 500;

    private readonly IMemoryStore _memory;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<RememberTool> _logger;

    public RememberTool(IMemoryStore memory, ICurrentUser currentUser, ILogger<RememberTool> logger)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static Tool Definition => new()
    {
        Name = Name,
        Description = ToolDescription,
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["content"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = ContentDescription,
                }),
                ["kind"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    @enum = AllowedKinds,
                    description = KindDescription,
                }),
            },
            Required = ["content"],
        },
    };

    /// <summary>Maps and validates tool input. Static and side-effect free for unit testing.</summary>
    public static bool TryParseInput(JsonElement input, out string content, out string kind, out string? error)
    {
        content = string.Empty;
        kind = DefaultKind;

        if (input.ValueKind is not JsonValueKind.Object)
        {
            error = "Tool input must be a JSON object.";
            return false;
        }

        if (!input.TryGetProperty("content", out var contentEl)
            || contentEl.ValueKind is not JsonValueKind.String
            || string.IsNullOrWhiteSpace(contentEl.GetString()))
        {
            error = "Missing or invalid required string property 'content'.";
            return false;
        }

        var parsedContent = contentEl.GetString()!.Trim();
        if (parsedContent.Length > MaxContentLength)
        {
            error = $"Property 'content' must be at most {MaxContentLength} characters.";
            return false;
        }

        if (input.TryGetProperty("kind", out var kindEl) && kindEl.ValueKind is not JsonValueKind.Null)
        {
            if (kindEl.ValueKind is not JsonValueKind.String
                || !AllowedKinds.Contains(kindEl.GetString()))
            {
                error = $"Property 'kind' must be one of: {string.Join(", ", AllowedKinds)}.";
                return false;
            }

            kind = kindEl.GetString()!;
        }

        content = parsedContent;
        error = null;
        return true;
    }

    public async Task<(string Content, bool IsError, Guid? TicketId, double? Confidence)> ExecuteAsync(
        JsonElement input,
        CancellationToken ct)
    {
        if (!TryParseInput(input, out var content, out var kind, out var parseError))
            return ($"Invalid tool input: {parseError}", true, null, null);

        MemoryWriteResult result;
        try
        {
            result = await _memory.AddAsync(_currentUser.Owner, content, kind, ct);
        }
        catch (Exception ex) when (TransientFault.IsTransient(ex, ct))
        {
            // Let the retry policy handle transient faults (e.g. Voyage 429/5xx).
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Storing a memory failed");
            return ("Storing the memory failed due to a technical error. Continue without it.", true, null, null);
        }

        if (!result.IsAvailable)
            return ("Long-term memory is not available in this deployment. Do not retry this tool; " +
                    "continue without storing.", false, null, null);

        return ($"Stored as a {kind} memory.", false, null, null);
    }
}
