using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using ServiceDeskLite.Contracts.V1.Assistant;
using ServiceDeskLite.Web.Api.V1.Assistant;

namespace ServiceDeskLite.Web.Features.Assistant.Pages;

public partial class AssistantChatPage : IDisposable
{
    [Inject] private IAssistantApiClient AssistantClient { get; set; } = default!;

    // Server-side tool name (ServiceDeskLite.Api.Assistant.CheckGroundingTool.Name); the web
    // renders its result as a grounding badge instead of a generic tool chip.
    private const string GroundingToolName = "check_grounding";

    private readonly List<ChatEntry> _entries = [];
    private readonly CancellationTokenSource _disposeCts = new();

    private MudBlazor.MudTextField<string> _inputRef = default!;
    private string? _input;
    private bool _isStreaming;
    private bool _hasStreamedText;

    // Set from the server's first `conversation` event; resent each turn so the
    // server keeps context without the client resending the whole transcript.
    private Guid? _conversationId;

    // KeyUp, not KeyDown: with Immediate binding, the input event that follows
    // keydown would re-populate the bound value with the old text after Clear().
    // By keyup time no further input events are pending.
    private async Task OnComposerKeyUpAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Enter" && !args.ShiftKey && !_isStreaming && !string.IsNullOrWhiteSpace(_input))
            await SendAsync();
    }

    private async Task SendAsync()
    {
        var message = _input?.Trim();
        if (string.IsNullOrWhiteSpace(message) || _isStreaming)
            return;

        _input = null;
        // Clear() syncs the field's internal text; just nulling the bound value
        // leaves the typed text visible while the field has focus.
        await _inputRef.Clear();
        _isStreaming = true;
        _hasStreamedText = false;
        _entries.Add(new ChatEntry(ChatEntryKind.User, message));
        StateHasChanged();

        // The server persists conversation state, so only the new user message is
        // sent; _conversationId ties this turn to the ongoing conversation.
        var newMessage = new AssistantChatMessage(AssistantChatRole.User, message);

        ChatEntry? assistantEntry = null;

        try
        {
            await foreach (var evt in AssistantClient.ChatAsync(_conversationId, newMessage, _disposeCts.Token))
            {
                switch (evt.EventType)
                {
                    case AssistantStreamEvent.ConversationEvent:
                        _conversationId = evt.ConversationId;
                        break;

                    case AssistantStreamEvent.TextEvent:
                        // Tool events may interleave with text; a new bubble starts after each interruption.
                        if (assistantEntry is null || _entries[^1] != assistantEntry)
                        {
                            assistantEntry = new ChatEntry(ChatEntryKind.Assistant, string.Empty);
                            _entries.Add(assistantEntry);
                        }

                        assistantEntry.Text += evt.Text;
                        _hasStreamedText = true;
                        break;

                    case AssistantStreamEvent.ToolCallEvent:
                        _entries.Add(new ChatEntry(ChatEntryKind.ToolCall, evt.ToolName ?? "tool"));
                        break;

                    case AssistantStreamEvent.ToolResultEvent:
                        if (evt.ToolName == GroundingToolName)
                            _entries.Add(new ChatEntry(ChatEntryKind.Grounding, evt.Message ?? string.Empty, score: evt.Confidence));
                        else
                            _entries.Add(new ChatEntry(
                                ChatEntryKind.ToolResult,
                                evt.Message ?? string.Empty,
                                evt.IsError == true ? null : evt.TicketId));
                        break;

                    case AssistantStreamEvent.CitationEvent:
                        if (evt.Citations is { Count: > 0 } citations)
                            _entries.Add(new ChatEntry(ChatEntryKind.Citations, string.Empty, citations: citations));
                        break;

                    case AssistantStreamEvent.ErrorEvent:
                        _entries.Add(new ChatEntry(ChatEntryKind.Error, evt.Message ?? "Unexpected error."));
                        break;
                }

                StateHasChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // page disposed mid-stream — nothing to render
        }
        catch (HttpRequestException)
        {
            _entries.Add(new ChatEntry(ChatEntryKind.Error, "The API is not reachable. Is it running?"));
        }
        finally
        {
            _isStreaming = false;
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }

    private enum ChatEntryKind
    {
        User,
        Assistant,
        ToolCall,
        ToolResult,
        Citations,
        Grounding,
        Error,
    }

    private sealed class ChatEntry(
        ChatEntryKind kind,
        string text,
        Guid? ticketId = null,
        IReadOnlyList<AssistantCitation>? citations = null,
        double? score = null)
    {
        public ChatEntryKind Kind { get; } = kind;
        public string Text { get; set; } = text;
        public Guid? TicketId { get; } = ticketId;
        public IReadOnlyList<AssistantCitation> Citations { get; } = citations ?? [];
        public double? Score { get; } = score;
    }
}
