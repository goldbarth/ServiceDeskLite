using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Abstractions.Persistence;

public interface ITicketRepository
{
    Task AddAsync(Ticket ticket,  CancellationToken ct = default);
    
    Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default);

    Task<bool> ExistsAsync(TicketId id, CancellationToken ct = default);
    
    Task<PagedResult<Ticket>> SearchAsync(
        TicketSearchCriteria criteria,
        Paging paging,
        SortSpec sort,
        CancellationToken ct = default);
}
