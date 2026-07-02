using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Web.Api.V1.Assistant;

public interface IAssistantApiClient
{
    /// <summary>Sends the full conversation transcript and streams the assistant's SSE response.</summary>
    IAsyncEnumerable<AssistantStreamEvent> ChatAsync(
        IReadOnlyList<AssistantChatMessage> transcript,
        CancellationToken ct = default);
}
