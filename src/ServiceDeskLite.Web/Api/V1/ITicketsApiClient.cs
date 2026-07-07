using ServiceDeskLite.Contracts.V1.Agents;
using ServiceDeskLite.Contracts.V1.Common;
using ServiceDeskLite.Contracts.V1.Dashboard;
using ServiceDeskLite.Contracts.V1.Tickets;

namespace ServiceDeskLite.Web.Api.V1;

public interface ITicketsApiClient
{
    Task<ApiResult<IReadOnlyList<AgentResponse>>> GetAgentsAsync(
        CancellationToken ct = default);

    Task<ApiResult<PagedResponse<TicketListItemResponse>>> SearchAsync(
        SearchTicketsRequest request,
        CancellationToken ct = default);
    
    Task<ApiResult<TicketResponse>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default);
    
    Task<ApiResult<CreateTicketResponse>> CreateAsync(
        CreateTicketRequest request,
        CancellationToken ct = default);

    Task<ApiResult<TicketResponse>> UpdateAsync(
        Guid id,
        UpdateTicketRequest request,
        CancellationToken ct = default);

    Task<ApiResult<TicketResponse>> ChangeStatusAsync(
        Guid id,
        ChangeTicketStatusRequest request,
        CancellationToken ct = default);

    Task<ApiResult<TicketResponse>> AssignAsync(
        Guid id,
        AssignTicketRequest request,
        CancellationToken ct = default);

    Task<ApiResult<CommentResponse>> AddCommentAsync(
        Guid id,
        AddCommentRequest request,
        CancellationToken ct = default);

    Task<ApiResult<IReadOnlyList<AuditEventResponse>>> GetAuditEventsAsync(
        Guid id,
        CancellationToken ct = default);

    Task<ApiResult<DashboardSummaryResponse>> GetDashboardSummaryAsync(
        CancellationToken ct = default);
}
