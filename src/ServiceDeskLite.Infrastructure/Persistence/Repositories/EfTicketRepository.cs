using Microsoft.EntityFrameworkCore;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;
using ServiceDeskLite.Infrastructure.Persistence.Configurations;

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
            // Case-insensitive substring match (ADR-0042). ILIKE folds case; the term is
            // escaped for LIKE metacharacters so a literal % / _ matches itself.
            var pattern = LikeContainsPattern(criteria.Text.Trim());
            q = q.Where(t => EF.Functions.ILike(t.Title, pattern) || EF.Functions.ILike(t.Description, pattern));
        }

        if (criteria.Statuses is { Count: > 0 })
            q = q.Where(t => criteria.Statuses.Contains(t.Status));

        if (criteria.Priorities is { Count: > 0 })
            q = q.Where(t => criteria.Priorities.Contains(t.Priority));

        if (!string.IsNullOrWhiteSpace(criteria.AssigneeName))
        {
            // Filter by the assigned agent's name via the roster (FK, ADR-0025). Same
            // case-insensitive rule as free-text search (ADR-0042).
            var namePattern = LikeContainsPattern(criteria.AssigneeName.Trim());
            q = q.Where(t => t.AssignedAgentId != null
                && _dbContext.Agents.Any(a => a.Id == t.AssignedAgentId && EF.Functions.ILike(a.Name, namePattern)));
        }

        if (criteria.Unassigned)
            q = q.Where(t => t.AssignedAgentId == null);

        if (criteria.Overdue)
        {
            // Same definition as the IsOverdue projection below, so the filter never
            // returns a row the list would render as not overdue.
            var now = _clock.UtcNow;
            q = q.Where(t => t.DueAt != null && t.DueAt < now
                && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);
        }

        var reference = TicketReference.Normalize(criteria.Reference);
        if (reference is not null)
        {
            // Match the display ref against a computed column (right("Id"::text, 6)); the
            // strongly-typed id can't be turned into text in a translatable LINQ expression.
            q = q.Where(t => EF.Property<string>(t, TicketConfiguration.RefSuffixColumn) == reference);
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
                t.Category,
                t.CreatedAt,
                t.DueAt,
                AssigneeName = _dbContext.Agents
                    .Where(a => a.Id == t.AssignedAgentId)
                    .Select(a => a.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var utcNow = _clock.UtcNow;
        var items = raw
            .Select(t => new TicketListItemDto(
                t.Id,
                t.Title,
                t.Status,
                t.Priority,
                t.Category,
                t.CreatedAt,
                t.DueAt,
                t.AssigneeName,
                TicketWorkflow.GetAllowedTransitions(t.Status),
                t.DueAt is not null && t.DueAt.Value < utcNow
                    && t.Status is not TicketStatus.Resolved and not TicketStatus.Closed,
                TicketReference.Format(t.Id)))
            .ToList();

        return new PagedResult<TicketListItemDto>(items, total, paging);
    }

    // Escape LIKE metacharacters in a user term (default '\' escape char) and wrap it as a
    // contains pattern, so `50%` searches for the literal text rather than a wildcard.
    private static string LikeContainsPattern(string term)
    {
        var escaped = term
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        return $"%{escaped}%";
    }
}
