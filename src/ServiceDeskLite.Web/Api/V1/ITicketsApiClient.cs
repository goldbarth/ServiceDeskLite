using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Tickets;

namespace ServiceDeskLite.Web.Api.V1;

public interface ITicketsApiClient
{
    Task<ApiResult<PagedResponse<TicketListItemResponse>>> SearchAsync(
        SearchTicketsRequest request,
        CancellationToken ct = default);
    
    Task<ApiResult<TicketResponse>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default);
    
    Task<ApiResult<CreateTicketResponse>> CreateAsync(
        CreateTicketRequest request,
        CancellationToken ct = default);
}
