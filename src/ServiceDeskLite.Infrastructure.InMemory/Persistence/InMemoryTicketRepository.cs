using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

internal sealed class InMemoryTicketRepository : ITicketRepository
{
    private readonly InMemoryStore _store;
    private readonly InMemoryUnitOfWork _unitOfWork;

    public InMemoryTicketRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork)
    {
        _store = store;
        _unitOfWork = unitOfWork;
    }
    
    public Task AddAsync(Ticket ticket, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _unitOfWork.PendingAdds.Add(ticket);
        return Task.CompletedTask;
    }

    public Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_store.TryGetTicket(id, out var ticket) ? ticket : null);
    }

    public Task<bool> ExistsAsync(TicketId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_store.ContainsTicket(id));
    }

    public Task<PagedResult<Ticket>> SearchAsync(TicketSearchCriteria criteria, Paging paging, SortSpec sort, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        IEnumerable<Ticket> q = _store.SnapshotTickets();
        
        if (!string.IsNullOrWhiteSpace(criteria.Text))
        {
            var term = criteria.Text.Trim();
            q = q.Where(t => t.Title.Contains(term) || t.Description.Contains(term));
        }

        if (criteria.Statuses is { Count: > 0 })
            q = q.Where(t => criteria.Statuses.Contains(t.Status));

        if (criteria.Priorities is { Count: > 0 })
            q = q.Where(t => criteria.Priorities.Contains(t.Priority));

        if (criteria.CreatedFrom is not null)
            q = q.Where(t => t.CreatedAt >= criteria.CreatedFrom);
        if (criteria.CreatedTo is not null)
            q = q.Where(t => t.CreatedAt <= criteria.CreatedTo);

        if (criteria.DueFrom is not null)
            q = q.Where(t => t.DueAt >= criteria.DueFrom);
        if (criteria.DueTo is not null)
            q = q.Where(t => t.DueAt <= criteria.DueTo);

        q = sort switch
        {
            { Field: TicketSortField.CreatedAt, Direction: SortDirection.Asc }  => q.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id.Value),
            { Field: TicketSortField.CreatedAt, Direction: SortDirection.Desc } => q.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id.Value),
            { Field: TicketSortField.DueAt, Direction: SortDirection.Asc }      => q.OrderBy(t => t.DueAt).ThenBy(t => t.Id.Value),
            { Field: TicketSortField.DueAt, Direction: SortDirection.Desc }     => q.OrderByDescending(t => t.DueAt).ThenBy(t => t.Id.Value),
            { Field: TicketSortField.Priority, Direction: SortDirection.Asc }   => q.OrderBy(t => t.Priority).ThenBy(t => t.Id.Value),
            { Field: TicketSortField.Priority, Direction: SortDirection.Desc }  => q.OrderByDescending(t => t.Priority).ThenBy(t => t.Id.Value),
            { Field: TicketSortField.Status, Direction: SortDirection.Asc }     => q.OrderBy(t => t.Status).ThenBy(t => t.Id.Value),
            { Field: TicketSortField.Status, Direction: SortDirection.Desc }    => q.OrderByDescending(t => t.Status).ThenBy(t => t.Id.Value),
            { Field: TicketSortField.Title, Direction: SortDirection.Asc }      => q.OrderBy(t => t.Title).ThenBy(t => t.Id.Value),
            { Field: TicketSortField.Title, Direction: SortDirection.Desc }     => q.OrderByDescending(t => t.Title).ThenBy(t => t.Id.Value),
            _ => q.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id.Value)
        };

        var enumerable = q as Ticket[] ?? q.ToArray();
        var total = enumerable.Length;

        var items = enumerable
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .ToList();

        var result = new PagedResult<Ticket>(items, total, paging);
        return Task.FromResult(result);
    }
}
