using System.Text.Json.Serialization;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Payload of a single SSE event on the assistant stream. The SSE event name
/// (conversation, text, tool_call, tool_result, error, done) is carried by
/// SseItem.EventType; unused fields are omitted from the JSON.
/// </summary>
public sealed record AssistantSseEvent(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ToolName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? TicketId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? IsError = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ConversationId = null)
{
    /// <summary>First event on every stream: carries the conversation id for the client's next turn.</summary>
    public const string ConversationEvent = "conversation";
    public const string TextEvent = "text";
    public const string ToolCallEvent = "tool_call";
    public const string ToolResultEvent = "tool_result";
    public const string ErrorEvent = "error";
    public const string DoneEvent = "done";
}
