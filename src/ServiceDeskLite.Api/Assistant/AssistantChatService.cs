using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;

using Anthropic.Models.Messages;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Api.Assistant.Agent;
using ServiceDeskLite.Api.Observability;
using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// The chat adapter: loads the conversation, runs <see cref="AgentLoop"/>, and translates what the
/// agent does into Server-Sent Events for the browser.
/// </summary>
/// <remarks>
/// The tool-calling loop itself lives in <see cref="AgentLoop"/>, shared with the autonomous worker
/// (ADR-0037). What remains here is everything that is true of a conversation and of nothing else:
/// server-side transcript state (ADR-0026), the date and timezone the model resolves "by Friday"
/// against, and the SSE shape the web client reads.
/// </remarks>
public sealed partial class AssistantChatService
{
    private const string UserRole = "user";
    private const string AssistantRole = "assistant";

    private readonly AgentLoop _agent;
    private readonly IConversationStore _conversations;
    private readonly ICurrentUser _currentUser;
    private readonly AnthropicOptions _options;
    private readonly IClock _clock;

    public AssistantChatService(
        AgentLoop agent,
        IConversationStore conversations,
        ICurrentUser currentUser,
        IOptions<AnthropicOptions> options,
        IClock clock)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _conversations = conversations ?? throw new ArgumentNullException(nameof(conversations));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async IAsyncEnumerable<SseItem<AssistantSseEvent>> StreamChatAsync(
        Guid? conversationId,
        AssistantChatMessage newMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var owner = _currentUser.Owner;
        var conversation = conversationId is { } id ? new ConversationId(id) : ConversationId.New();

        // Parent of every model turn and tool call in this request, so one trace shows the whole
        // chain of decisions rather than a handful of unrelated spans.
        using var chatActivity = AssistantInstrumentation.ActivitySource.StartActivity("assistant.chat");
        chatActivity?.SetTag("conversation.id", conversation.Value);
        chatActivity?.SetTag("owner.id", owner.Value);

        // First event: hand the client the conversation id so its next turn sends
        // only the id + new message instead of the whole transcript.
        yield return new SseItem<AssistantSseEvent>(
            new AssistantSseEvent(ConversationId: conversation.Value), AssistantSseEvent.ConversationEvent);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.UserTimeZone);
        var localNow = TimeZoneInfo.ConvertTime(_clock.UtcNow, timeZone);
        var systemPrompt = BuildSystemPrompt(localNow, _options.UserTimeZone);

        var stored = await _conversations.GetAsync(conversation, owner, ct);

        var seed = stored
            .Select(m => new MessageParam
            {
                Role = m.Role == AssistantRole ? Role.Assistant : Role.User,
                Content = m.Content,
            })
            .ToList();

        seed.Add(new MessageParam { Role = Role.User, Content = newMessage.Content });

        var nextSequence = stored.Count;
        var request = new AgentRequest(systemPrompt, seed, owner, _options.MaxTokens);

        await foreach (var step in _agent.RunAsync(request, ct))
        {
            switch (step)
            {
                case AgentTextEvent text:
                    yield return new SseItem<AssistantSseEvent>(
                        new AssistantSseEvent(Text: text.Text), AssistantSseEvent.TextEvent);
                    break;

                case AgentToolCallEvent call:
                    yield return new SseItem<AssistantSseEvent>(
                        new AssistantSseEvent(ToolName: call.ToolName), AssistantSseEvent.ToolCallEvent);
                    break;

                case AgentToolResultEvent tool:
                    yield return new SseItem<AssistantSseEvent>(
                        new AssistantSseEvent(
                            ToolName: tool.ToolName, TicketId: tool.Result.TicketId, IsError: tool.Result.IsError,
                            Message: tool.Result.Content, Confidence: tool.Result.Confidence),
                        AssistantSseEvent.ToolResultEvent);

                    // Surface knowledge-base sources as a distinct event so the client can show
                    // where the answer came from. Emitted only for sources actually retrieved.
                    if (tool.Result.Citations is { Count: > 0 } citations)
                        yield return new SseItem<AssistantSseEvent>(
                            new AssistantSseEvent(ToolName: tool.ToolName, Citations: citations),
                            AssistantSseEvent.CitationEvent);
                    break;

                case AgentCompletedEvent completed:
                    await PersistTurnAsync(
                        conversation, owner, nextSequence, newMessage.Content, completed.Text, ct);
                    yield return new SseItem<AssistantSseEvent>(new AssistantSseEvent(), AssistantSseEvent.DoneEvent);
                    yield break;

                case AgentErrorEvent error:
                    yield return new SseItem<AssistantSseEvent>(
                        new AssistantSseEvent(Message: error.Message), AssistantSseEvent.ErrorEvent);
                    yield break;
            }
        }
    }

    /// <summary>
    /// Persists this turn's user message and the assistant's final text. Called only
    /// on successful completion — a failed or aborted turn is left unpersisted so the
    /// client can retry the same conversation without a dangling user message. Only
    /// text turns are stored (matching the prior client-resend fidelity); the in-loop
    /// tool_use/tool_result blocks stay request-local.
    /// </summary>
    private async Task PersistTurnAsync(
        ConversationId conversation, OwnerId owner, int startSequence,
        string userText, string assistantText, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        List<ConversationMessage> toPersist = [new(startSequence, UserRole, userText, now)];
        if (!string.IsNullOrEmpty(assistantText))
            toPersist.Add(new(startSequence + 1, AssistantRole, assistantText, now));

        await _conversations.AppendAsync(conversation, owner, toPersist, ct);
    }
}
