using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.InMemory.Persistence;

internal sealed class InMemoryTicketRepository : ITicketRepository
{
    private readonly InMemoryStore _store;
    private readonly InMemoryUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public InMemoryTicketRepository(InMemoryStore store, InMemoryUnitOfWork unitOfWork, IClock clock)
    {
        _store = store;
        _unitOfWork = unitOfWork;
        _clock = clock;
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

    public Task<PagedResult<TicketListItemDto>> SearchAsync(TicketSearchCriteria criteria, Paging paging, SortSpec sort, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        IEnumerable<Ticket> q = _store.SnapshotTickets();

        // Resolve assignee display names from the agent roster (FK, ADR-0025).
        var agentNames = _store.SnapshotAgents().ToDictionary(a => a.Id, a => a.Name);

        if (!string.IsNullOrWhiteSpace(criteria.Text))
        {
            var term = criteria.Text.Trim();
            q = q.Where(t => t.Title.Contains(term) || t.Description.Contains(term));
        }

        if (criteria.Statuses is { Count: > 0 })
            q = q.Where(t => criteria.Statuses.Contains(t.Status));

        if (criteria.Priorities is { Count: > 0 })
            q = q.Where(t => criteria.Priorities.Contains(t.Priority));

        if (!string.IsNullOrWhiteSpace(criteria.AssigneeName))
        {
            var name = criteria.AssigneeName.Trim();
            q = q.Where(t => t.AssignedAgentId is { } id
                              && agentNames.TryGetValue(id, out var n)
                              && n.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        if (criteria.Unassigned)
            q = q.Where(t => t.AssignedAgentId is null);

        if (criteria.Overdue)
        {
            // Same definition as the IsOverdue projection below, so the filter never
            // returns a row the list would render as not overdue.
            var now = _clock.UtcNow;
            q = q.Where(t => t.DueAt is not null && t.DueAt.Value < now
                && t.Status is not TicketStatus.Resolved and not TicketStatus.Closed);
        }

        var reference = TicketReference.Normalize(criteria.Reference);
        if (reference is not null)
            q = q.Where(t => TicketReference.Suffix(t.Id) == reference);

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

        var utcNow = _clock.UtcNow;
        var items = enumerable
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(t => new TicketListItemDto(
                t.Id,
                t.Title,
                t.Status,
                t.Priority,
                t.Category,
                t.CreatedAt,
                t.DueAt,
                t.AssignedAgentId is { } aid && agentNames.TryGetValue(aid, out var an) ? an : null,
                TicketWorkflow.GetAllowedTransitions(t.Status),
                t.DueAt is not null && t.DueAt.Value < utcNow
                    && t.Status is not TicketStatus.Resolved and not TicketStatus.Closed,
                TicketReference.Format(t.Id)))
            .ToList();

        return Task.FromResult(new PagedResult<TicketListItemDto>(items, total, paging));
    }
}
