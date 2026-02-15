namespace ServiceDeskLite.Application.Tickets.Shared;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    Paging Paging);
