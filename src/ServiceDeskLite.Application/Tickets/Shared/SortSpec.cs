namespace ServiceDeskLite.Application.Tickets.Shared;

public enum SortDirection { Asc, Desc }

public enum TicketSortField
{
    CreatedAt,
    DueAt,
    Priority,
    Status,
    Title
}

public readonly record struct SortSpec(TicketSortField Field, SortDirection Direction)
{
    public static SortSpec Default => new(TicketSortField.CreatedAt, SortDirection.Desc);
}
