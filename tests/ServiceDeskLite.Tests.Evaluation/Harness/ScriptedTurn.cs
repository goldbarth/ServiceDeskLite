using System.Text;
using System.Text.Json;

namespace ServiceDeskLite.Tests.Evaluation.Harness;

/// <summary>
/// One scripted model response, rendered as the Server-Sent Events the Anthropic API would emit.
/// </summary>
/// <remarks>
/// The scenarios describe what the model decides; this turns that decision into the exact wire
/// format the SDK parses. Writing the wire rather than stubbing the SDK means the suite also
/// covers the SDK's stream parsing and our own delta accumulation — the two places where a
/// silent change would otherwise reach production unnoticed.
/// </remarks>
public sealed class ScriptedTurn
{
    private readonly List<string> _textBlocks = [];
    private readonly List<ToolUse> _toolUses = [];

    public long InputTokens { get; private set; } = 100;
    public long OutputTokens { get; private set; } = 20;

    public static ScriptedTurn Create() => new();

    /// <summary>Text the model streams. Each call is one content block; deltas are emitted per word.</summary>
    public ScriptedTurn Says(string text)
    {
        _textBlocks.Add(text);
        return this;
    }

    /// <summary>A tool the model requests. <paramref name="input"/> is serialized as its arguments.</summary>
    public ScriptedTurn Calls(string toolName, object input)
    {
        _toolUses.Add(new ToolUse($"toolu_{_toolUses.Count + 1}", toolName, JsonSerializer.Serialize(input)));
        return this;
    }

    /// <summary>Raw arguments, for scenarios that need malformed or oversized input.</summary>
    public ScriptedTurn CallsRaw(string toolName, string inputJson)
    {
        _toolUses.Add(new ToolUse($"toolu_{_toolUses.Count + 1}", toolName, inputJson));
        return this;
    }

    public ScriptedTurn Costing(long inputTokens, long outputTokens)
    {
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        return this;
    }

    /// <summary>Renders the turn. Stop reason follows from the content: a tool was requested, or it did not.</summary>
    public string ToSse()
    {
        var sse = new StringBuilder();
        var stopReason = _toolUses.Count > 0 ? "tool_use" : "end_turn";
        var index = 0;

        Event(sse, "message_start", new
        {
            type = "message_start",
            message = new
            {
                id = "msg_scripted",
                type = "message",
                role = "assistant",
                model = "claude-opus-4-8",
                content = Array.Empty<object>(),
                stop_reason = (string?)null,
                stop_sequence = (string?)null,
                usage = new { input_tokens = InputTokens, output_tokens = 1 },
            },
        });

        foreach (var text in _textBlocks)
        {
            Event(sse, "content_block_start", new
            {
                type = "content_block_start",
                index,
                content_block = new { type = "text", text = "" },
            });

            // One delta per word: a client that only works when the whole text arrives at once
            // would pass a single-delta test and fail against the real API.
            foreach (var chunk in SplitIntoDeltas(text))
            {
                Event(sse, "content_block_delta", new
                {
                    type = "content_block_delta",
                    index,
                    delta = new { type = "text_delta", text = chunk },
                });
            }

            Event(sse, "content_block_stop", new { type = "content_block_stop", index });
            index++;
        }

        foreach (var tool in _toolUses)
        {
            Event(sse, "content_block_start", new
            {
                type = "content_block_start",
                index,
                content_block = new { type = "tool_use", id = tool.Id, name = tool.Name, input = new { } },
            });

            // Arguments arrive as partial JSON fragments, split mid-token, exactly as they do live.
            foreach (var fragment in SplitIntoFragments(tool.InputJson))
            {
                Event(sse, "content_block_delta", new
                {
                    type = "content_block_delta",
                    index,
                    delta = new { type = "input_json_delta", partial_json = fragment },
                });
            }

            Event(sse, "content_block_stop", new { type = "content_block_stop", index });
            index++;
        }

        Event(sse, "message_delta", new
        {
            type = "message_delta",
            delta = new { stop_reason = stopReason, stop_sequence = (string?)null },
            usage = new { input_tokens = InputTokens, output_tokens = OutputTokens },
        });

        Event(sse, "message_stop", new { type = "message_stop" });

        return sse.ToString();
    }

    private static void Event(StringBuilder sse, string name, object payload) =>
        sse.Append("event: ").Append(name).Append('\n')
            .Append("data: ").Append(JsonSerializer.Serialize(payload)).Append("\n\n");

    private static IEnumerable<string> SplitIntoDeltas(string text)
    {
        var words = text.Split(' ');
        for (var i = 0; i < words.Length; i++)
            yield return i == 0 ? words[i] : " " + words[i];
    }

    private static IEnumerable<string> SplitIntoFragments(string json)
    {
        const int size = 7;
        for (var i = 0; i < json.Length; i += size)
            yield return json.Substring(i, Math.Min(size, json.Length - i));
    }

    private sealed record ToolUse(string Id, string Name, string InputJson);
}
