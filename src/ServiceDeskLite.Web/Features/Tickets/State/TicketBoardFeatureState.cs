using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;

namespace ServiceDeskLite.Web.Features.Tickets.State;

/// <summary>
/// Scoped service that owns the ticket board state machine.
/// One instance lives per SignalR circuit (browser tab), surviving page navigation.
///
/// Lifecycle:
///   Idle → Loading → Loaded | Error
///                   ↑
///           ReloadAsync / MoveAsync (on success)
/// </summary>
public sealed class TicketBoardFeatureState : IDisposable
{
    private readonly ITicketsApiClient _api;

    private TicketBoardState _state = new TicketBoardState.Idle();
    private bool _hideClosed;

    private CancellationTokenSource? _cts;
    private long _seq;

    public TicketBoardState State => _state;

    /// <summary>
    /// When true, tickets in status Closed are excluded from the board view.
    /// Toggling this flag does not trigger a server round-trip.
    /// </summary>
    public bool HideClosed => _hideClosed;

    /// <summary>
    /// Raised on every state transition. Subscribers call StateHasChanged in response.
    /// </summary>
    public event Action? OnChanged;

    public TicketBoardFeatureState(ITicketsApiClient api) => _api = api;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Loads all tickets from the server (up to the API maximum of 200).</summary>
    public async Task LoadAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var seq = Interlocked.Increment(ref _seq);

        SetState(new TicketBoardState.Loading());

        try
        {
            var result = await _api.SearchAsync(
                new SearchTicketsRequest(Page: 1, PageSize: 200),
                _cts.Token);

            if (Interlocked.Read(ref _seq) != seq)
                return; // superseded by a newer LoadAsync call

            SetState(result.IsSuccess
                ? new TicketBoardState.Loaded(result.Value!.Items)
                : new TicketBoardState.Error(result.Error!));
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the next LoadAsync call — state will be updated by that call.
        }
        catch (Exception ex)
        {
            if (Interlocked.Read(ref _seq) != seq)
                return;

            SetState(new TicketBoardState.Error(
                new ApiError { Status = 500, Title = "Unexpected error", Detail = ex.Message }));
        }
    }

    /// <summary>
    /// Attempts to move a ticket to a new status by calling the backend.
    /// Returns null on success (board reloads automatically).
    /// Returns an <see cref="ApiError"/> when the backend rejects the transition
    /// so the caller can display it to the user.
    /// </summary>
    public async Task<ApiError?> MoveAsync(Guid ticketId, TicketStatus newStatus)
    {
        var result = await _api.ChangeStatusAsync(
            ticketId,
            new ChangeTicketStatusRequest(newStatus));

        if (!result.IsSuccess)
            return result.Error;

        await LoadAsync();
        return null;
    }

    /// <summary>
    /// Toggles visibility of Closed tickets without a server round-trip.
    /// </summary>
    public void ToggleHideClosed()
    {
        _hideClosed = !_hideClosed;
        OnChanged?.Invoke();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private void SetState(TicketBoardState state)
    {
        _state = state;
        OnChanged?.Invoke();
    }
}