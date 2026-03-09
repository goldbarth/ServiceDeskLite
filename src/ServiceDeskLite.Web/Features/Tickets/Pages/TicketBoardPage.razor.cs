using Microsoft.AspNetCore.Components;

using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;
using ServiceDeskLite.Web.Features.Tickets.State;

namespace ServiceDeskLite.Web.Features.Tickets.Pages;

public partial class TicketBoardPage : IDisposable
{
    [Inject] private TicketBoardFeatureState BoardState { get; set; } = default!;

    private static readonly TicketStatus[] AllColumns =
    [
        TicketStatus.New,
        TicketStatus.Triaged,
        TicketStatus.InProgress,
        TicketStatus.Waiting,
        TicketStatus.Resolved,
        TicketStatus.Closed,
    ];

    private TicketListItemResponse? _draggingTicket;
    private bool _isDragging;
    private ApiError? _moveError;

    private IEnumerable<TicketStatus> VisibleColumns =>
        BoardState.HideClosed
            ? AllColumns.Where(s => s != TicketStatus.Closed)
            : AllColumns;

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        BoardState.OnChanged += StateHasChanged;
        await BoardState.LoadAsync();
    }

    private void OnHideClosedChanged(bool _) => BoardState.ToggleHideClosed();

    public void Dispose() => BoardState.OnChanged -= StateHasChanged;

    // -----------------------------------------------------------------------
    // Drag & Drop handlers
    // -----------------------------------------------------------------------

    private void OnDragStart(TicketListItemResponse ticket)
    {
        _draggingTicket = ticket;
        _isDragging = true;
        _moveError = null;
    }

    private void OnDragEnd()
    {
        _draggingTicket = null;
        _isDragging = false;
    }

    private async Task OnDrop(TicketStatus targetStatus)
    {
        if (_draggingTicket is null) return;

        // Dropping onto the same column is a no-op.
        if (_draggingTicket.Status == targetStatus) return;

        var ticket = _draggingTicket;
        _draggingTicket = null;
        _isDragging = false;

        // Pessimistic move: ask the backend first, reload on success.
        // The backend enforces all domain transition rules — we never duplicate them here.
        _moveError = await BoardState.MoveAsync(ticket.Id, targetStatus);
    }

    // -----------------------------------------------------------------------
    // View helpers
    // -----------------------------------------------------------------------

    private static IReadOnlyList<TicketListItemResponse> TicketsForColumn(
        IReadOnlyList<TicketListItemResponse> tickets,
        TicketStatus status)
        => [.. tickets.Where(t => t.Status == status)];

    private static string FormatStatus(TicketStatus status) => status switch
    {
        TicketStatus.InProgress => "In Progress",
        _ => status.ToString(),
    };
}
