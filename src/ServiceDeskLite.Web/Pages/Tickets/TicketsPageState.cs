using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;
using ServiceDeskLite.Web.Api.V1;

namespace ServiceDeskLite.Web.Pages.Tickets;

public sealed class TicketsPageState
{
    public abstract record TicketsState;

    public sealed record LoadingState : TicketsState;
    public sealed record EmptyState : TicketsState;
    public sealed record LoadedState(PagedResponse<TicketListItemResponse> Data) : TicketsState;
    public sealed record ErrorState(ApiError Error) : TicketsState;
}
