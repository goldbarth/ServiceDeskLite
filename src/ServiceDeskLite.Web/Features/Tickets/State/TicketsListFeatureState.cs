using ServiceDeskLite.Web.Api.V1;

namespace ServiceDeskLite.Web.Features.Tickets.State;

/// <summary>
/// Scoped service that owns the tickets list state machine.
/// One instance lives per SignalR circuit (browser tab), surviving page navigation.
///
/// Lifecycle:
///   Idle → Loading → Loaded | Error
///                  ↑
///          ReloadAsync / Invalidate
/// </summary>
public sealed class TicketsListFeatureState : IDisposable
{
    private readonly ITicketsApiClient _api;

    private TicketQueryParams _query = TicketQueryParams.Default;
    private TicketsListState _state = new TicketsListState.Idle();
    private bool _isStale;

    // Cancels the currently in-flight HTTP request when a newer load supersedes it.
    private CancellationTokenSource? _cts;

    // Monotonically increasing counter. After each await we verify the counter still
    // matches — if not, a newer LoadAsync call has already taken over and we discard
    // this result silently.
    private long _seq;

    public TicketQueryParams Query => _query;
    public TicketsListState State => _state;

    /// <summary>
    /// True when a mutation has signalled that the current data is outdated.
    /// The list page checks this on mount to decide whether to reload.
    /// </summary>
    public bool IsStale => _isStale;

    /// <summary>
    /// Raised on every state transition. Subscribers call StateHasChanged in response.
    /// </summary>
    public event Action? OnChanged;

    public TicketsListFeatureState(ITicketsApiClient api) => _api = api;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public async Task LoadAsync(TicketQueryParams query)
    {
        // Cancel any in-flight request — the new load supersedes it.
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        var seq = Interlocked.Increment(ref _seq);

        _query = query;
        _isStale = false;
        SetState(new TicketsListState.Loading());

        try
        {
            var result = await _api.SearchAsync(query.ToSearchRequest(), _cts.Token);

            if (Interlocked.Read(ref _seq) != seq)
                return; // superseded — a newer LoadAsync is already running

            SetState(result.IsSuccess
                ? new TicketsListState.Loaded(result.Value!)
                : new TicketsListState.Error(result.Error!));
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the next LoadAsync call — state will be updated by that call.
        }
        catch (Exception ex)
        {
            if (Interlocked.Read(ref _seq) != seq)
                return;

            // Convert unexpected exceptions to a synthetic ApiError so the UI
            // always deals with a single error type.
            SetState(new TicketsListState.Error(
                new ApiError { Status = 500, Title = "Unexpected error", Detail = ex.Message }));
        }
    }

    /// <summary>Reload using the current query parameters.</summary>
    public Task ReloadAsync() => LoadAsync(_query);

    /// <summary>
    /// Called by mutations (ChangeStatus, Assign, AddComment, Create) to signal
    /// that the list data is outdated.
    ///
    /// If the list page is currently mounted (has subscribers), a reload is triggered
    /// immediately. If not, the stale flag is set — the page will reload on next mount.
    /// </summary>
    public void Invalidate()
    {
        _isStale = true;

        if (OnChanged is not null)
            _ = ReloadAsync(); // fire-and-forget: safe because LoadAsync catches all exceptions
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private void SetState(TicketsListState state)
    {
        _state = state;
        OnChanged?.Invoke();
    }
}
