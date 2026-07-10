using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;

using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

using Microsoft.Extensions.Options;

using ServiceDeskLite.Api.Assistant.Sandbox;
using ServiceDeskLite.Application.Abstractions.Assistant;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.GetTicketById;

namespace ServiceDeskLite.Api.Assistant;

/// <summary>
/// Streams a structured, read-only summary of one ticket. Unlike
/// <see cref="AssistantChatService"/> this is a single model call with no tools: the
/// summary informs an agent, it never acts, so the model needs no way to reach the
/// application layer. Text deltas are routed through <see cref="SummarySectionParser"/>
/// and re-emitted as per-section SSE events.
/// </summary>
public sealed partial class TicketSummaryService
{
    private readonly AnthropicClient _client;
    private readonly IAssistantMetricsSink _metrics;
    private readonly ModelTurnLimiter _modelTurns;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly AnthropicOptions _options;
    private readonly ILogger<TicketSummaryService> _logger;

    public TicketSummaryService(
        AnthropicClient client,
        IAssistantMetricsSink metrics,
        ModelTurnLimiter modelTurns,
        ICurrentUser currentUser,
        IClock clock,
        IOptions<AnthropicOptions> options,
        ILogger<TicketSummaryService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _modelTurns = modelTurns ?? throw new ArgumentNullException(nameof(modelTurns));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async IAsyncEnumerable<SseItem<TicketSummarySseEvent>> StreamSummaryAsync(
        TicketDetailsDto ticket,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        // A summary is a billable model call like any other, and the tab streams one on every
        // first open. It shares the owner's model-turn budget with the chat.
        if (!_modelTurns.TryConsume(_currentUser.Owner))
        {
            _logger.LogWarning("Model-turn rate limit reached while summarizing ticket {TicketId}", ticket.Id.Value);
            yield return ErrorItem(_modelTurns.LimitMessage);
            yield break;
        }

        var parameters = new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = _options.SummaryMaxTokens,
            // See AgentLoop: SummaryMaxTokens is deliberately tight, thinking would eat into it.
            Thinking = new ThinkingConfigDisabled(),
            System = BuildSystemPrompt(),
            Messages = [new MessageParam { Role = Role.User, Content = BuildTicketPrompt(ticket) }],
        };

        var parser = new SummarySectionParser();
        long inputTokens = 0, outputTokens = 0;
        var stream = _client.Messages.CreateStreaming(parameters, cancellationToken: ct)
            .GetAsyncEnumerator(ct);

        // Manual enumeration: C# forbids `yield return` inside a catch block, so
        // MoveNextAsync is guarded separately and errors surface as SSE events.
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
                _logger.LogError(ex, "Anthropic API error while summarizing ticket {TicketId}", ticket.Id.Value);
                failed = true;
            }
            catch (Exception ex)
            {
                // Same contract as the chat stream: an unexpected upstream fault surfaces as an
                // error event, never as an exception thrown into a half-written response.
                _logger.LogError(ex, "Unexpected error while summarizing ticket {TicketId}", ticket.Id.Value);
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

            // A summary costs tokens like any other turn; leaving it out would understate
            // what the assistant actually spends.
            if (stream.Current.TryPickDelta(out var messageDelta) && messageDelta.Usage is { } usage)
            {
                inputTokens = usage.InputTokens ?? 0;
                outputTokens = usage.OutputTokens;
            }

            if (!TryReadTextDelta(stream.Current, out var text))
                continue;

            foreach (var delta in parser.Feed(text))
                yield return DeltaItem(delta);
        }

        await _metrics.RecordTokenUsageAsync(
            new AssistantTokenUsage(_options.Model, inputTokens, outputTokens, _clock.UtcNow), ct);

        foreach (var delta in parser.Flush())
            yield return DeltaItem(delta);

        yield return new SseItem<TicketSummarySseEvent>(
            new TicketSummarySseEvent(), TicketSummarySseEvent.DoneEvent);
    }

    private static bool TryReadTextDelta(RawMessageStreamEvent streamEvent, out string text)
    {
        if (streamEvent.TryPickContentBlockDelta(out var delta) &&
            delta.Delta.TryPickText(out TextDelta? textDelta))
        {
            text = textDelta.Text;
            return true;
        }

        text = string.Empty;
        return false;
    }

    private static SseItem<TicketSummarySseEvent> DeltaItem(SummaryDelta delta) =>
        new(new TicketSummarySseEvent(delta.Section, delta.Text), TicketSummarySseEvent.DeltaEvent);

    private static SseItem<TicketSummarySseEvent> ErrorItem(string message) =>
        new(new TicketSummarySseEvent(Message: message), TicketSummarySseEvent.ErrorEvent);
}
