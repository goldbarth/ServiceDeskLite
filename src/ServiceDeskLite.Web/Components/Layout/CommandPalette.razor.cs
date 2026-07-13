using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;
using ServiceDeskLite.Web.Composition;

namespace ServiceDeskLite.Web.Components.Layout;

public sealed partial class CommandPalette : IAsyncDisposable
{
    private const int DebounceMilliseconds = 200;
    private const int MaxTicketResults = 6;

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ITicketsApiClient TicketsApi { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private ElementReference _inputRef;
    private IJSObjectReference? _module;
    private DotNetObjectReference<CommandPalette>? _selfRef;

    private bool _open;
    private bool _focusPending;
    private string _query = string.Empty;
    private int _selectedIndex;

    private IReadOnlyList<TicketListItemResponse> _ticketResults = [];
    private IReadOnlyList<PaletteEntry> _entries = [];

    private CancellationTokenSource? _searchCts;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./Components/Layout/CommandPalette.razor.js");
            _selfRef = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("register", _selfRef);
        }

        if (_focusPending)
        {
            _focusPending = false;
            await _inputRef.FocusAsync();
        }
    }

    /// <summary>Invoked from JS when the user presses Ctrl/Cmd+K anywhere in the app.</summary>
    [JSInvokable]
    public async Task OpenFromJs()
    {
        if (_open)
            return;

        // Record the current focus before the input takes it, so Esc can hand it back.
        if (_module is not null)
            await _module.InvokeVoidAsync("saveFocus");

        _open = true;
        _query = string.Empty;
        _ticketResults = [];
        _selectedIndex = 0;
        RebuildEntries();
        _focusPending = true;

        StateHasChanged();
    }

    private async Task OnInputAsync(ChangeEventArgs args)
    {
        _query = args.Value?.ToString() ?? string.Empty;
        _selectedIndex = 0;

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;

        if (string.IsNullOrWhiteSpace(_query))
        {
            _ticketResults = [];
            RebuildEntries();
            return;
        }

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        var token = cts.Token;

        try
        {
            // Debounce: hold the request until typing pauses, then let the newest keystroke win.
            await Task.Delay(DebounceMilliseconds, token);

            var result = await TicketsApi.SearchAsync(
                new SearchTicketsRequest(Page: 1, PageSize: MaxTicketResults, Q: _query.Trim()),
                token);

            if (token.IsCancellationRequested)
                return;

            _ticketResults = result.IsSuccess ? result.Value!.Items : [];
            RebuildEntries();
            StateHasChanged();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer keystroke — its request owns the results.
        }
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "Escape":
                await CloseAsync(restoreFocus: true);
                break;

            case "ArrowDown":
                Move(1);
                break;

            case "ArrowUp":
                Move(-1);
                break;

            case "Enter":
                await ActivateSelectedAsync();
                break;
        }
    }

    private void Move(int delta)
    {
        if (_entries.Count == 0)
            return;

        _selectedIndex = Math.Clamp(_selectedIndex + delta, 0, _entries.Count - 1);
    }

    private async Task OnEntryClickAsync(int index)
    {
        _selectedIndex = index;
        await ActivateSelectedAsync();
    }

    private async Task ActivateSelectedAsync()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
            return;

        var target = _entries[_selectedIndex].Href;
        await CloseAsync(restoreFocus: false);
        Navigation.NavigateTo(target);
    }

    private async Task CloseAsync(bool restoreFocus)
    {
        _open = false;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;

        if (restoreFocus && _module is not null)
            await _module.InvokeVoidAsync("restoreFocus");

        StateHasChanged();
    }

    private string ResultClass(int index)
        => index == _selectedIndex
            ? "command-palette__result command-palette__result--active"
            : "command-palette__result";

    private void RebuildEntries()
    {
        _entries = CommandPaletteEntries.Build(_query, NavRegistry.Items, _ticketResults);

        if (_selectedIndex >= _entries.Count)
            _selectedIndex = Math.Max(0, _entries.Count - 1);
    }

    public async ValueTask DisposeAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already gone — nothing to clean up on the client.
            }
        }

        _selfRef?.Dispose();
    }
}
