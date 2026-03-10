using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Abstractions.Persistence;

public interface ITicketRepository
{
    Task AddAsync(Ticket ticket,  CancellationToken ct = default);

    Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default);

    Task<bool> ExistsAsync(TicketId id, CancellationToken ct = default);

    /// <summary>
    /// Returns a projected page of list-view DTOs.
    /// Only the fields required for <see cref="TicketListItemDto"/> are fetched —
    /// no full entity materialisation occurs.
    /// </summary>
    Task<PagedResult<TicketListItemDto>> SearchAsync(
        TicketSearchCriteria criteria,
        Paging paging,
        SortSpec sort,
        CancellationToken ct = default);
}
