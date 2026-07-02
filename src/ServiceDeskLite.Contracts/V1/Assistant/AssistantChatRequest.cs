using System.Text.Json.Serialization;

namespace ServiceDeskLite.Contracts.V1.Assistant;

/// <summary>
/// Full conversation transcript, oldest first. The API is stateless — the client
/// resends the history on every turn so the model keeps context (e.g. the id of
/// a ticket it created earlier).
/// </summary>
public sealed record AssistantChatRequest(IReadOnlyList<AssistantChatMessage> Messages);

public sealed record AssistantChatMessage(AssistantChatRole Role, string Content);

[JsonConverter(typeof(JsonStringEnumConverter<AssistantChatRole>))]
public enum AssistantChatRole
{
    User,
    Assistant,
}
