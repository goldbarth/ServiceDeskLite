using Microsoft.EntityFrameworkCore;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Infrastructure.Persistence.Repositories;

public class EfTicketRepository : ITicketRepository
{
    private readonly ServiceDeskLiteDbContext _dbContext;
    private readonly IClock _clock;

    public EfTicketRepository(ServiceDeskLiteDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public Task AddAsync(Ticket ticket, CancellationToken ct = default)
        => _dbContext.Tickets.AddAsync(ticket, ct)
            .AsTask();

    public Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default)
        => _dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<bool> ExistsAsync(TicketId id, CancellationToken ct)
        => _dbContext.Tickets.AnyAsync(t => t.Id == id, ct);

    public async Task<PagedResult<TicketListItemDto>> SearchAsync(TicketSearchCriteria criteria, Paging paging, SortSpec sort, CancellationToken ct = default)
    {
        IQueryable<Ticket> q = _dbContext.Tickets.AsNoTracking();

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
            q = q.Where(t => t.Assignee != null && t.Assignee.Value.Name.Contains(name));
        }

        if (criteria.CreatedFrom is not null)
            q = q.Where(t => t.CreatedAt >= criteria.CreatedFrom);
        if (criteria.CreatedTo is not null)
            q = q.Where(t => t.CreatedAt <= criteria.CreatedTo);

        if (criteria.DueFrom is not null)
            q = q.Where(t => t.DueAt >= criteria.DueFrom);
        if (criteria.DueTo is not null)
            q = q.Where(t => t.DueAt <= criteria.DueTo);

        var total = await q.CountAsync(ct);

        q = sort switch
        {
            { Field: TicketSortField.CreatedAt, Direction: SortDirection.Asc }  => q.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id),
            { Field: TicketSortField.CreatedAt, Direction: SortDirection.Desc } => q.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id),
            { Field: TicketSortField.DueAt, Direction: SortDirection.Asc }      => q.OrderBy(t => t.DueAt).ThenBy(t => t.Id),
            { Field: TicketSortField.DueAt, Direction: SortDirection.Desc }     => q.OrderByDescending(t => t.DueAt).ThenBy(t => t.Id),
            { Field: TicketSortField.Priority, Direction: SortDirection.Asc }   => q.OrderBy(t => t.Priority).ThenBy(t => t.Id),
            { Field: TicketSortField.Priority, Direction: SortDirection.Desc }  => q.OrderByDescending(t => t.Priority).ThenBy(t => t.Id),
            { Field: TicketSortField.Status, Direction: SortDirection.Asc }     => q.OrderBy(t => t.Status).ThenBy(t => t.Id),
            { Field: TicketSortField.Status, Direction: SortDirection.Desc }    => q.OrderByDescending(t => t.Status).ThenBy(t => t.Id),
            { Field: TicketSortField.Title, Direction: SortDirection.Asc }      => q.OrderBy(t => t.Title).ThenBy(t => t.Id),
            { Field: TicketSortField.Title, Direction: SortDirection.Desc }     => q.OrderByDescending(t => t.Title).ThenBy(t => t.Id),
            _ => q.OrderByDescending(t => t.CreatedAt)
        };

        var raw = await q
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Status,
                t.Priority,
                t.CreatedAt,
                t.DueAt,
                AssigneeName = t.Assignee != null ? t.Assignee.Value.Name : null
            })
            .ToListAsync(ct);

        var utcNow = _clock.UtcNow;
        var items = raw
            .Select(t => new TicketListItemDto(
                t.Id,
                t.Title,
                t.Status,
                t.Priority,
                t.CreatedAt,
                t.DueAt,
                t.AssigneeName,
                TicketWorkflow.GetAllowedTransitions(t.Status),
                t.DueAt is not null && t.DueAt.Value < utcNow
                    && t.Status is not TicketStatus.Resolved and not TicketStatus.Closed))
            .ToList();

        return new PagedResult<TicketListItemDto>(items, total, paging);
    }
}
