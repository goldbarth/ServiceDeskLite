using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using ServiceDeskLite.Contracts.V1.Assistant;
using ServiceDeskLite.Web.Api.V1.Assistant;

namespace ServiceDeskLite.Web.Features.Assistant.Pages;

public partial class AssistantChatPage : IDisposable
{
    [Inject] private IAssistantApiClient AssistantClient { get; set; } = default!;

    private readonly List<ChatEntry> _entries = [];
    private readonly CancellationTokenSource _disposeCts = new();

    private MudBlazor.MudTextField<string> _inputRef = default!;
    private string? _input;
    private bool _isStreaming;
    private bool _hasStreamedText;

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

        // The API is stateless — resend the visible conversation so the model keeps
        // context across turns (e.g. the id of a ticket it created earlier).
        var transcript = _entries
            .Where(e => e.Kind is ChatEntryKind.User or ChatEntryKind.Assistant && !string.IsNullOrWhiteSpace(e.Text))
            .Select(e => new AssistantChatMessage(
                e.Kind == ChatEntryKind.User ? AssistantChatRole.User : AssistantChatRole.Assistant,
                e.Text))
            .ToList();

        ChatEntry? assistantEntry = null;

        try
        {
            await foreach (var evt in AssistantClient.ChatAsync(transcript, _disposeCts.Token))
            {
                switch (evt.EventType)
                {
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
                        _entries.Add(new ChatEntry(
                            ChatEntryKind.ToolResult,
                            evt.Message ?? string.Empty,
                            evt.IsError == true ? null : evt.TicketId));
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
        Error,
    }

    private sealed class ChatEntry(ChatEntryKind kind, string text, Guid? ticketId = null)
    {
        public ChatEntryKind Kind { get; } = kind;
        public string Text { get; set; } = text;
        public Guid? TicketId { get; } = ticketId;
    }
}
