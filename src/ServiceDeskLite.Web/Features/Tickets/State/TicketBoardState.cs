using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;

namespace ServiceDeskLite.Web.Features.Tickets.State;

/// <summary>
/// Discriminated union representing every possible state of the ticket board.
/// Only one variant can be active at a time — no illegal state combinations possible.
/// </summary>
public abstract record TicketBoardState
{
    private TicketBoardState() { }

    /// <summary>Initial state before any load has been requested.</summary>
    public sealed record Idle : TicketBoardState;

    /// <summary>A load is in progress.</summary>
    public sealed record Loading : TicketBoardState;

    /// <summary>
    /// The last load succeeded. <see cref="Tickets"/> contains all tickets
    /// across all statuses — grouping is performed in the view.
    /// </summary>
    public sealed record Loaded(IReadOnlyList<TicketListItemResponse> Tickets) : TicketBoardState;

    /// <summary>
    /// The last load failed. Unexpected exceptions are converted to a synthetic
    /// ApiError with Status=500 before reaching this variant.
    /// </summary>
    public sealed record Error(ApiError ApiError) : TicketBoardState;
}