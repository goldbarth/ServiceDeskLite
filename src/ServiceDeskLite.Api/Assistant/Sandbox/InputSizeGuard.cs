using System.Text.Json;

using Microsoft.Extensions.Options;

namespace ServiceDeskLite.Api.Assistant.Sandbox;

/// <summary>
/// Caps the size of tool arguments before any tool parses them.
/// Tool input is model output shaped by ticket text the model read, and ticket text is
/// user-supplied. This is the first place that untrusted content is measured rather than
/// trusted, and it runs before a handler ever sees it.
/// </summary>
public sealed class InputSizeGuard : IToolGuard
{
    private readonly AgentSandboxOptions _options;

    public InputSizeGuard(IOptions<AgentSandboxOptions> options)
        => _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public ToolGuardResult Check(ToolInvocationContext context)
    {
        var raw = context.Input.GetRawText();
        if (raw.Length > _options.MaxInputCharacters)
        {
            return ToolGuardResult.Deny(
                $"Tool arguments are too large ({raw.Length} characters, limit {_options.MaxInputCharacters}). "
                + "Shorten the input and call the tool again.");
        }

        return FindOversizedString(context.Input) is { } property
            ? ToolGuardResult.Deny(
                $"Property '{property}' exceeds the {_options.MaxStringCharacters}-character limit. "
                + "Summarize it and call the tool again.")
            : ToolGuardResult.Allow();
    }

    /// <returns>The name of the first string property over the limit, or null.</returns>
    private string? FindOversizedString(JsonElement element, string path = "")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString()?.Length > _options.MaxStringCharacters
                    ? (path.Length == 0 ? "(root)" : path)
                    : null;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var name = path.Length == 0 ? property.Name : $"{path}.{property.Name}";
                    if (FindOversizedString(property.Value, name) is { } found)
                        return found;
                }

                return null;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (FindOversizedString(item, $"{path}[{index++}]") is { } found)
                        return found;
                }

                return null;

            default:
                return null;
        }
    }
}
