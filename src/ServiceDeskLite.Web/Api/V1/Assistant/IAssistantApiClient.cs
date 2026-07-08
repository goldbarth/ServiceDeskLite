using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Web.Api.V1.Assistant;

public interface IAssistantApiClient
{
    /// <summary>
    /// Sends one new user message and streams the assistant's SSE response. Pass the
    /// <paramref name="conversationId"/> from a prior turn's <c>conversation</c> event
    /// to continue that conversation; pass <c>null</c> to start a new one.
    /// </summary>
    IAsyncEnumerable<AssistantStreamEvent> ChatAsync(
        Guid? conversationId,
        AssistantChatMessage newMessage,
        CancellationToken ct = default);
}
