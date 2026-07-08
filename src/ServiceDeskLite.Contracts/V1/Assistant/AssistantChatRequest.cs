using System.Text.Json.Serialization;

namespace ServiceDeskLite.Contracts.V1.Assistant;

/// <summary>
/// One chat turn. The server persists conversation state, so the client sends
/// only the new user message plus the <see cref="ConversationId"/> it received
/// on the first turn — no full-transcript resend. Omit <see cref="ConversationId"/>
/// to start a new conversation; the server returns the new id on the stream
/// (the <c>conversation</c> SSE event).
/// </summary>
public sealed record AssistantChatRequest(Guid? ConversationId, AssistantChatMessage NewMessage);

public sealed record AssistantChatMessage(AssistantChatRole Role, string Content);

[JsonConverter(typeof(JsonStringEnumConverter<AssistantChatRole>))]
public enum AssistantChatRole
{
    User,
    Assistant,
}
