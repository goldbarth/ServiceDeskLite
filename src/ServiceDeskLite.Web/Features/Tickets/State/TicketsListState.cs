using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;

namespace ServiceDeskLite.Web.Features.Tickets.State;

/// <summary>
/// Discriminated union representing every possible state of the tickets list.
/// Only one variant can be active at a time — no illegal state combinations possible.
/// </summary>
public abstract record TicketsListState
{
    private TicketsListState() { }

    /// <summary>Initial state before any load has been requested.</summary>
    public sealed record Idle : TicketsListState;

    /// <summary>A load is in progress. Previous data (if any) is no longer shown.</summary>
    public sealed record Loading : TicketsListState;

    /// <summary>
    /// The last load succeeded. <see cref="Data"/> may contain zero items —
    /// the template decides how to render the empty case.
    /// </summary>
    public sealed record Loaded(PagedResponse<TicketListItemResponse> Data) : TicketsListState;

    /// <summary>
    /// The last load failed. Unexpected exceptions are converted to a synthetic
    /// ApiError with Status=500 before reaching this variant.
    /// </summary>
    public sealed record Error(ApiError ApiError) : TicketsListState;
}
