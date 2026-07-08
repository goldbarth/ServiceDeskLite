namespace ServiceDeskLite.Web.Api.V1.Assistant;

/// <summary>
/// One event from the assistant SSE stream. EventType mirrors the server-side
/// event names: conversation, text, tool_call, tool_result, error, done.
/// </summary>
public sealed record AssistantStreamEvent(
    string EventType,
    string? Text = null,
    string? ToolName = null,
    Guid? TicketId = null,
    bool? IsError = null,
    string? Message = null,
    Guid? ConversationId = null)
{
    public const string ConversationEvent = "conversation";
    public const string TextEvent = "text";
    public const string ToolCallEvent = "tool_call";
    public const string ToolResultEvent = "tool_result";
    public const string ErrorEvent = "error";
    public const string DoneEvent = "done";
}
