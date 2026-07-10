using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Api.Assistant.Sandbox;
using ServiceDeskLite.Api.Observability;
using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Api.Assistant.Agent;

/// <summary>
/// The agentic tool-calling loop: call the model, stream its text, execute the tools it asks for,
/// feed the results back, and repeat until it stops asking.
/// </summary>
/// <remarks>
/// One loop, two callers (ADR-0037). <see cref="AssistantChatService"/> drives it for a person
/// waiting on an SSE stream; the autonomous worker drives it with nobody watching. Everything that
/// bounds the agent — the guard pipeline, the model-turn budget, tool retries, the metrics and the
/// decision spans — lives here, so it cannot hold for one caller and not the other. That is exactly
/// how a second, parallel loop would have drifted.
/// <para>
/// The loop never throws across its boundary. An upstream fault, an exhausted budget, or a
/// malformed response all leave as an <see cref="AgentErrorEvent"/>.
/// </para>
/// </remarks>
public sealed class AgentLoop
{
    private readonly AnthropicClient _client;
    private readonly ToolDispatcher _tools;
    private readonly IAssistantMetricsSink _metrics;
    private readonly ToolGuardPipeline _guards;
    private readonly ModelTurnLimiter _modelTurns;
    private readonly IAgentActor _agent;
    private readonly AnthropicOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<AgentLoop> _logger;

    public AgentLoop(
        AnthropicClient client,
        ToolDispatcher tools,
        IAssistantMetricsSink metrics,
        ToolGuardPipeline guards,
        ModelTurnLimiter modelTurns,
        IAgentActor agent,
        IOptions<AnthropicOptions> options,
        IClock clock,
        ILogger<AgentLoop> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _guards = guards ?? throw new ArgumentNullException(nameof(guards));
        _modelTurns = modelTurns ?? throw new ArgumentNullException(nameof(modelTurns));
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async IAsyncEnumerable<AgentLoopEvent> RunAsync(
        AgentRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = request.Seed.ToList();

        // Accumulates the model's text across all tool iterations of this run; a caller that
        // persists a transcript wants the whole answer, not the last fragment.
        var answer = new StringBuilder();
        var turnState = new ToolTurnState();

        for (var iteration = 0; iteration <= _options.MaxToolIterations; iteration++)
        {
            // Checked per round trip, not per run: a long tool chain is several billable calls,
            // and the budget must see each of them.
            if (!_modelTurns.TryConsume(request.Owner))
            {
                _logger.LogWarning("Model-turn rate limit reached for owner {Owner}", request.Owner.Value);
                yield return new AgentErrorEvent(_modelTurns.LimitMessage);
                yield break;
            }

            var parameters = new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = request.MaxTokens,
                // Sonnet 5 runs adaptive thinking when the field is absent. Thinking tokens
                // would count against MaxTokens, which a long tool chain already fills.
                Thinking = new ThinkingConfigDisabled(),
                System = request.SystemPrompt,
                Tools = [.. ToolDispatcher.Definitions],
                Messages = messages,
            };

            using var turnActivity = AssistantInstrumentation.ActivitySource.StartActivity("assistant.model_turn");
            turnActivity?.SetTag("model", _options.Model);
            turnActivity?.SetTag("turn.iteration", iteration);
            turnActivity?.SetTag("agent.mode", _agent.Mode.ToString());

            var turn = new TurnAccumulator();
            var stream = _client.Messages.CreateStreaming(parameters, cancellationToken: ct)
                .GetAsyncEnumerator(ct);

            // Manual enumeration: C# forbids `yield return` inside a catch block, so MoveNextAsync
            // is guarded separately and errors surface as events.
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
                    // caller went away — nothing left to send
                }
                catch (AnthropicApiException ex)
                {
                    _logger.LogError(ex, "Anthropic API error during agent run");
                    failed = true;
                }
                catch (Exception ex)
                {
                    // Nothing guarantees the upstream response is shaped as documented. The loop
                    // promises it never breaks, so an unexpected fault becomes an error event
                    // rather than an unhandled exception mid-run.
                    _logger.LogError(ex, "Unexpected error while reading the agent stream");
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
                    yield return new AgentErrorEvent("The AI service is currently unavailable. Please try again.");
                    yield break;
                }

                if (!moved)
                    break;

                var textDelta = turn.Apply(stream.Current);
                if (textDelta is not null)
                {
                    answer.Append(textDelta);
                    yield return new AgentTextEvent(textDelta);
                }
            }

            // A turn that ended without a stop reason produced nothing the API promises. Completing
            // here would hand the caller an empty answer and call it success.
            if (turn.StopReason is null)
            {
                _logger.LogError("Agent stream ended without a stop reason; the response was incomplete");
                yield return new AgentErrorEvent("The AI service returned an incomplete response. Please try again.");
                yield break;
            }

            turnActivity?.SetTag("tokens.input", turn.InputTokens);
            turnActivity?.SetTag("tokens.output", turn.OutputTokens);
            turnActivity?.SetTag("stop_reason", turn.StopReason?.ToString());

            // One run can span several model round trips; each is billed on its own.
            await _metrics.RecordTokenUsageAsync(
                new AssistantTokenUsage(_options.Model, turn.InputTokens, turn.OutputTokens, _clock.UtcNow), ct);

            if (turn.StopReason != StopReason.ToolUse || turn.ToolCalls.Count == 0)
            {
                yield return new AgentCompletedEvent(answer.ToString());
                yield break;
            }

            if (iteration == _options.MaxToolIterations)
            {
                _logger.LogWarning("Agent exceeded max tool iterations ({Max})", _options.MaxToolIterations);
                yield return new AgentErrorEvent("The assistant exceeded the maximum number of tool calls.");
                yield break;
            }

            // Echo the assistant turn, execute each requested tool, and return all tool_result
            // blocks in a single user message.
            messages.Add(new MessageParam { Role = Role.Assistant, Content = turn.ToAssistantContent() });

            List<ContentBlockParam> toolResults = [];
            foreach (var call in turn.ToolCalls)
            {
                yield return new AgentToolCallEvent(call.Name);

                var (result, duration) = await RunToolAsync(call, request.Owner, turnState, ct);

                await _metrics.RecordToolInvocationAsync(
                    new AssistantToolInvocation(
                        ToolName: call.Name,
                        Kind: ToolCatalog.KindOf(call.Name),
                        IsError: result.IsError,
                        Confidence: result.Confidence,
                        MatchCount: result.MatchCount,
                        SemanticAvailable: result.SemanticAvailable,
                        Duration: duration,
                        OccurredAt: _clock.UtcNow),
                    ct);

                yield return new AgentToolResultEvent(call.Name, result);

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

    private TimeSpan Backoff(int attempt)
    {
        var ms = _options.ToolRetryBaseDelayMs * Math.Pow(2, attempt);
        return TimeSpan.FromMilliseconds(Math.Min(ms, 30_000));
    }

    /// <summary>
    /// Admits, executes and times one tool call, and records the decision as a trace span.
    /// </summary>
    /// <remarks>
    /// The span is where "which tool, and why" becomes answerable after the fact: it carries the
    /// tool, its kind, whether a guard refused it and on what grounds, the confidence a retrieval
    /// reported, and the ticket a write touched. Duration is measured here rather than around the
    /// handler, so it is what the caller waited for, retries included.
    /// </remarks>
    private async Task<(ToolResult Result, TimeSpan Duration)> RunToolAsync(
        ToolCall call, OwnerId owner, ToolTurnState turnState, CancellationToken ct)
    {
        var kind = ToolCatalog.KindOf(call.Name);

        using var activity = AssistantInstrumentation.ActivitySource.StartActivity("assistant.tool");
        activity?.SetTag("tool.name", call.Name);
        activity?.SetTag("tool.kind", kind.ToString());
        activity?.SetTag("agent.mode", _agent.Mode.ToString());

        var startedAt = Stopwatch.GetTimestamp();

        // Admission runs before execution, so a refused call never reaches a command handler.
        // A refusal is an ordinary error tool_result: the model reads the reason and adjusts,
        // exactly as it does for a validation failure.
        var admission = _guards.Admit(new ToolInvocationContext(call.Name, call.Input, owner, turnState, _agent.Mode));

        ToolResult result;
        if (admission.IsAllowed)
        {
            result = await ToolRetryPolicy.ExecuteAsync(
                c => _tools.ExecuteAsync(call.Name, call.Input, c), _options.MaxToolRetries, Backoff, _logger, ct);
        }
        else
        {
            activity?.SetTag("tool.refused_by_guard", true);
            activity?.SetTag("tool.refusal_reason", admission.Reason);
            result = new ToolResult(admission.Reason!, IsError: true);
        }

        var duration = Stopwatch.GetElapsedTime(startedAt);

        activity?.SetTag("tool.is_error", result.IsError);
        activity?.SetTag("tool.duration_ms", duration.TotalMilliseconds);

        if (result.Confidence is { } confidence)
            activity?.SetTag("tool.confidence", confidence);

        if (result.MatchCount is { } matches)
            activity?.SetTag("tool.match_count", matches);

        if (result.TicketId is { } ticketId)
            activity?.SetTag("ticket.id", ticketId);

        if (result.IsError)
            activity?.SetStatus(ActivityStatusCode.Error, result.Content);

        _logger.LogInformation(
            "Tool {Tool} ({Kind}) finished in {Duration:F0} ms; error={IsError}, confidence={Confidence}",
            call.Name, kind, duration.TotalMilliseconds, result.IsError, result.Confidence);

        return (result, duration);
    }

    private sealed record ToolCall(string Id, string Name, JsonElement Input);

    /// <summary>
    /// Assembles one assistant turn from raw stream events: text deltas are concatenated, tool_use
    /// input arrives as partial JSON fragments that are buffered per block and parsed once the
    /// block stops.
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

        public long InputTokens { get; private set; }
        public long OutputTokens { get; private set; }

        /// <returns>The text delta to forward to the caller, if this event carried one.</returns>
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

            if (streamEvent.TryPickDelta(out var messageDelta))
            {
                if (messageDelta.Delta.StopReason is { } stopReason)
                    StopReason = stopReason;

                // The final message_delta reports this turn's cumulative token cost. It is the only
                // place the API states it, so it is the only place to read it.
                if (messageDelta.Usage is { } usage)
                {
                    InputTokens = usage.InputTokens ?? 0;
                    OutputTokens = usage.OutputTokens;
                }
            }

            return null;
        }

        public List<ContentBlockParam> ToAssistantContent() => _content;
    }
}
