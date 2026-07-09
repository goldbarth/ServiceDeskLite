using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Contracts.V1.Assistant;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Drives the Anthropic Messages API tool-calling loop with streaming:
/// text deltas are forwarded as SSE events while tool_use blocks are
/// assembled from partial-JSON deltas, executed against the application
/// layer, and fed back to the model until it stops requesting tools.
/// </summary>
public sealed partial class AssistantChatService
{
    private const string UserRole = "user";
    private const string AssistantRole = "assistant";

    private readonly AnthropicClient _client;
    private readonly CreateTicketTool _createTool;
    private readonly UpdateTicketTool _updateTool;
    private readonly FindSimilarTicketsTool _findSimilarTool;
    private readonly SearchTicketsTool _searchTool;
    private readonly ChangeTicketStatusTool _changeStatusTool;
    private readonly AssignTicketTool _assignTool;
    private readonly RouteTicketTool _routeTool;
    private readonly SearchKnowledgeBaseTool _knowledgeTool;
    private readonly CheckGroundingTool _groundingTool;
    private readonly RememberTool _rememberTool;
    private readonly RecallMemoryTool _recallTool;
    private readonly IConversationStore _conversations;
    private readonly ICurrentUser _currentUser;
    private readonly AnthropicOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<AssistantChatService> _logger;

    public AssistantChatService(
        AnthropicClient client,
        CreateTicketTool createTool,
        UpdateTicketTool updateTool,
        FindSimilarTicketsTool findSimilarTool,
        SearchTicketsTool searchTool,
        ChangeTicketStatusTool changeStatusTool,
        AssignTicketTool assignTool,
        RouteTicketTool routeTool,
        SearchKnowledgeBaseTool knowledgeTool,
        CheckGroundingTool groundingTool,
        RememberTool rememberTool,
        RecallMemoryTool recallTool,
        IConversationStore conversations,
        ICurrentUser currentUser,
        IOptions<AnthropicOptions> options,
        IClock clock,
        ILogger<AssistantChatService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _createTool = createTool ?? throw new ArgumentNullException(nameof(createTool));
        _updateTool = updateTool ?? throw new ArgumentNullException(nameof(updateTool));
        _findSimilarTool = findSimilarTool ?? throw new ArgumentNullException(nameof(findSimilarTool));
        _searchTool = searchTool ?? throw new ArgumentNullException(nameof(searchTool));
        _changeStatusTool = changeStatusTool ?? throw new ArgumentNullException(nameof(changeStatusTool));
        _assignTool = assignTool ?? throw new ArgumentNullException(nameof(assignTool));
        _routeTool = routeTool ?? throw new ArgumentNullException(nameof(routeTool));
        _knowledgeTool = knowledgeTool ?? throw new ArgumentNullException(nameof(knowledgeTool));
        _groundingTool = groundingTool ?? throw new ArgumentNullException(nameof(groundingTool));
        _rememberTool = rememberTool ?? throw new ArgumentNullException(nameof(rememberTool));
        _recallTool = recallTool ?? throw new ArgumentNullException(nameof(recallTool));
        _conversations = conversations ?? throw new ArgumentNullException(nameof(conversations));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async IAsyncEnumerable<SseItem<AssistantSseEvent>> StreamChatAsync(
        Guid? conversationId,
        AssistantChatMessage newMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var owner = _currentUser.Owner;
        var conversation = conversationId is { } id ? new ConversationId(id) : ConversationId.New();

        // First event: hand the client the conversation id so its next turn sends
        // only the id + new message instead of the whole transcript.
        yield return new SseItem<AssistantSseEvent>(
            new AssistantSseEvent(ConversationId: conversation.Value), AssistantSseEvent.ConversationEvent);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.UserTimeZone);
        var localNow = TimeZoneInfo.ConvertTime(_clock.UtcNow, timeZone);
        var systemPrompt = BuildSystemPrompt(localNow, _options.UserTimeZone);

        var stored = await _conversations.GetAsync(conversation, owner, ct);

        var messages = stored
            .Select(m => new MessageParam
            {
                Role = m.Role == AssistantRole ? Role.Assistant : Role.User,
                Content = m.Content,
            })
            .ToList();

        messages.Add(new MessageParam { Role = Role.User, Content = newMessage.Content });

        // Accumulates the assistant's text across all tool iterations of this turn;
        // persisted (with the user message) once the model produces its final answer.
        var assistantText = new StringBuilder();
        var nextSequence = stored.Count;

        for (var iteration = 0; iteration <= _options.MaxToolIterations; iteration++)
        {
            var parameters = new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = _options.MaxTokens,
                System = systemPrompt,
                Tools =
                [
                    CreateTicketTool.Definition,
                    UpdateTicketTool.Definition,
                    FindSimilarTicketsTool.Definition,
                    SearchTicketsTool.Definition,
                    ChangeTicketStatusTool.Definition,
                    AssignTicketTool.Definition,
                    RouteTicketTool.Definition,
                    SearchKnowledgeBaseTool.Definition,
                    CheckGroundingTool.Definition,
                    RememberTool.Definition,
                    RecallMemoryTool.Definition,
                ],
                Messages = messages,
            };

            var turn = new TurnAccumulator();
            var stream = _client.Messages.CreateStreaming(parameters, cancellationToken: ct)
                .GetAsyncEnumerator(ct);

            // Manual enumeration: C# forbids `yield return` inside a catch block,
            // so MoveNextAsync is guarded separately and errors surface as SSE events.
            while (true)
            {
                var moved = false;
                var failed = false;
                try
                {
                    moved = await stream.MoveNextAsync();
                }
                catch (OperationCanceledException)
                {
                    // client disconnected — nothing left to send
                }
                catch (AnthropicApiException ex)
                {
                    _logger.LogError(ex, "Anthropic API error during assistant stream");
                    failed = true;
                }
                finally
                {
                    if (!moved)
                        await stream.DisposeAsync();
                }

                if (ct.IsCancellationRequested)
                    yield break;

                if (failed)
                {
                    yield return ErrorItem("The AI service is currently unavailable. Please try again.");
                    yield break;
                }

                if (!moved)
                    break;

                var textDelta = turn.Apply(stream.Current);
                if (textDelta is not null)
                {
                    assistantText.Append(textDelta);
                    yield return new SseItem<AssistantSseEvent>(
                        new AssistantSseEvent(Text: textDelta), AssistantSseEvent.TextEvent);
                }
            }

            if (turn.StopReason != StopReason.ToolUse || turn.ToolCalls.Count == 0)
            {
                await PersistTurnAsync(conversation, owner, nextSequence, newMessage.Content, assistantText.ToString(), ct);
                yield return new SseItem<AssistantSseEvent>(new AssistantSseEvent(), AssistantSseEvent.DoneEvent);
                yield break;
            }

            if (iteration == _options.MaxToolIterations)
            {
                _logger.LogWarning("Assistant exceeded max tool iterations ({Max})", _options.MaxToolIterations);
                yield return ErrorItem("The assistant exceeded the maximum number of tool calls.");
                yield break;
            }

            // Echo the assistant turn, execute each requested tool, and return
            // all tool_result blocks in a single user message.
            messages.Add(new MessageParam { Role = Role.Assistant, Content = turn.ToAssistantContent() });

            List<ContentBlockParam> toolResults = [];
            foreach (var call in turn.ToolCalls)
            {
                yield return new SseItem<AssistantSseEvent>(
                    new AssistantSseEvent(ToolName: call.Name), AssistantSseEvent.ToolCallEvent);

                var result = await ToolRetryPolicy.ExecuteAsync(
                    c => ExecuteToolAsync(call, c), _options.MaxToolRetries, Backoff, _logger, ct);

                if (result.Confidence is { } score)
                    _logger.LogInformation("Tool {Tool} returned confidence {Confidence:P0}.", call.Name, score);

                yield return new SseItem<AssistantSseEvent>(
                    new AssistantSseEvent(
                        ToolName: call.Name, TicketId: result.TicketId, IsError: result.IsError,
                        Message: result.Content, Confidence: result.Confidence),
                    AssistantSseEvent.ToolResultEvent);

                // Surface knowledge-base sources as a distinct event so the client can show
                // where the answer came from. Emitted only for sources actually retrieved.
                if (result.Citations is { Count: > 0 } citations)
                    yield return new SseItem<AssistantSseEvent>(
                        new AssistantSseEvent(ToolName: call.Name, Citations: citations),
                        AssistantSseEvent.CitationEvent);

                toolResults.Add(new ToolResultBlockParam
                {
                    ToolUseID = call.Id,
                    Content = result.Content,
                    IsError = result.IsError,
                });
            }

            messages.Add(new MessageParam { Role = Role.User, Content = toolResults });
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

    private TimeSpan Backoff(int attempt)
    {
        var ms = _options.ToolRetryBaseDelayMs * Math.Pow(2, attempt);
        return TimeSpan.FromMilliseconds(Math.Min(ms, 30_000));
    }

    private Task<ToolResult> ExecuteToolAsync(ToolCall call, CancellationToken ct) =>
        call.Name switch
        {
            CreateTicketTool.Name => _createTool.ExecuteAsync(call.Input, ct),
            UpdateTicketTool.Name => _updateTool.ExecuteAsync(call.Input, _clock.UtcNow, ct),
            FindSimilarTicketsTool.Name => _findSimilarTool.ExecuteAsync(call.Input, ct),
            SearchTicketsTool.Name => _searchTool.ExecuteAsync(call.Input, ct),
            ChangeTicketStatusTool.Name => _changeStatusTool.ExecuteAsync(call.Input, ct),
            AssignTicketTool.Name => _assignTool.ExecuteAsync(call.Input, ct),
            RouteTicketTool.Name => _routeTool.ExecuteAsync(call.Input, ct),
            SearchKnowledgeBaseTool.Name => _knowledgeTool.ExecuteAsync(call.Input, ct),
            CheckGroundingTool.Name => _groundingTool.ExecuteAsync(call.Input, ct),
            RememberTool.Name => _rememberTool.ExecuteAsync(call.Input, ct),
            RecallMemoryTool.Name => _recallTool.ExecuteAsync(call.Input, ct),
            _ => Task.FromResult(new ToolResult($"Unknown tool '{call.Name}'.", true)),
        };

    private static SseItem<AssistantSseEvent> ErrorItem(string message) =>
        new(new AssistantSseEvent(Message: message), AssistantSseEvent.ErrorEvent);

    private sealed record ToolCall(string Id, string Name, JsonElement Input);

    /// <summary>
    /// Assembles one assistant turn from raw stream events: text deltas are
    /// concatenated, tool_use input arrives as partial JSON fragments that are
    /// buffered per block and parsed once the block stops.
    /// </summary>
    private sealed class TurnAccumulator
    {
        private readonly List<ContentBlockParam> _content = [];
        private readonly StringBuilder _textBuffer = new();
        private readonly StringBuilder _toolJsonBuffer = new();
        private string? _toolId;
        private string? _toolName;
        private bool _inTextBlock;

        public List<ToolCall> ToolCalls { get; } = [];
        public StopReason? StopReason { get; private set; }

        /// <returns>The text delta to forward to the client, if this event carried one.</returns>
        public string? Apply(RawMessageStreamEvent streamEvent)
        {
            if (streamEvent.TryPickContentBlockStart(out var start))
            {
                if (start.ContentBlock.TryPickToolUse(out ToolUseBlock? toolUse))
                {
                    _toolId = toolUse.ID;
                    _toolName = toolUse.Name;
                    _toolJsonBuffer.Clear();
                }
                else if (start.ContentBlock.TryPickText(out TextBlock? _))
                {
                    _inTextBlock = true;
                    _textBuffer.Clear();
                }

                return null;
            }

            if (streamEvent.TryPickContentBlockDelta(out var delta))
            {
                if (delta.Delta.TryPickText(out TextDelta? text))
                {
                    _textBuffer.Append(text.Text);
                    return text.Text;
                }

                if (delta.Delta.TryPickInputJson(out InputJsonDelta? inputJson))
                    _toolJsonBuffer.Append(inputJson.PartialJson);

                return null;
            }

            if (streamEvent.TryPickContentBlockStop(out _))
            {
                if (_toolId is not null && _toolName is not null)
                {
                    // Empty input streams as zero fragments; normalize to "{}".
                    var json = _toolJsonBuffer.Length > 0 ? _toolJsonBuffer.ToString() : "{}";
                    var input = JsonSerializer.Deserialize<JsonElement>(json);

                    ToolCalls.Add(new ToolCall(_toolId, _toolName, input));
                    _content.Add(new ToolUseBlockParam
                    {
                        ID = _toolId,
                        Name = _toolName,
                        Input = input.EnumerateObject().ToDictionary(p => p.Name, p => p.Value),
                    });

                    _toolId = null;
                    _toolName = null;
                    _toolJsonBuffer.Clear();
                }
                else if (_inTextBlock)
                {
                    if (_textBuffer.Length > 0)
                        _content.Add(new TextBlockParam { Text = _textBuffer.ToString() });

                    _inTextBlock = false;
                    _textBuffer.Clear();
                }

                return null;
            }

            if (streamEvent.TryPickDelta(out var messageDelta) && messageDelta.Delta.StopReason is { } stopReason)
                StopReason = stopReason;

            return null;
        }

        public List<ContentBlockParam> ToAssistantContent() => _content;
    }
}
