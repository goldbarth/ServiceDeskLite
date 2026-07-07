using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

using Microsoft.Extensions.Options;

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
    private readonly AnthropicClient _client;
    private readonly CreateTicketTool _createTool;
    private readonly UpdateTicketTool _updateTool;
    private readonly FindSimilarTicketsTool _findSimilarTool;
    private readonly SearchTicketsTool _searchTool;
    private readonly ChangeTicketStatusTool _changeStatusTool;
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
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async IAsyncEnumerable<SseItem<AssistantSseEvent>> StreamChatAsync(
        IReadOnlyList<AssistantChatMessage> transcript,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.UserTimeZone);
        var localNow = TimeZoneInfo.ConvertTime(_clock.UtcNow, timeZone);
        var systemPrompt = BuildSystemPrompt(localNow, _options.UserTimeZone);

        var messages = transcript
            .Select(m => new MessageParam
            {
                Role = m.Role == AssistantChatRole.Assistant ? Role.Assistant : Role.User,
                Content = m.Content,
            })
            .ToList();

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
                    yield return new SseItem<AssistantSseEvent>(
                        new AssistantSseEvent(Text: textDelta), AssistantSseEvent.TextEvent);
            }

            if (turn.StopReason != StopReason.ToolUse || turn.ToolCalls.Count == 0)
            {
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

                var (content, isError, ticketId) = await ExecuteToolAsync(call, ct);

                yield return new SseItem<AssistantSseEvent>(
                    new AssistantSseEvent(ToolName: call.Name, TicketId: ticketId, IsError: isError, Message: content),
                    AssistantSseEvent.ToolResultEvent);

                toolResults.Add(new ToolResultBlockParam
                {
                    ToolUseID = call.Id,
                    Content = content,
                    IsError = isError,
                });
            }

            messages.Add(new MessageParam { Role = Role.User, Content = toolResults });
        }
    }

    private Task<(string Content, bool IsError, Guid? TicketId)> ExecuteToolAsync(ToolCall call, CancellationToken ct) =>
        call.Name switch
        {
            CreateTicketTool.Name => _createTool.ExecuteAsync(call.Input, ct),
            UpdateTicketTool.Name => _updateTool.ExecuteAsync(call.Input, _clock.UtcNow, ct),
            FindSimilarTicketsTool.Name => _findSimilarTool.ExecuteAsync(call.Input, ct),
            SearchTicketsTool.Name => _searchTool.ExecuteAsync(call.Input, ct),
            ChangeTicketStatusTool.Name => _changeStatusTool.ExecuteAsync(call.Input, ct),
            _ => Task.FromResult(($"Unknown tool '{call.Name}'.", true, (Guid?)null)),
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
