namespace ServiceDeskLite.Application.Tickets.Shared;

public readonly record struct Paging(int Page, int PageSize)
{
    public static Paging Default => new(Page: 1, PageSize: 25);
    
    public int Skip => (Page - 1) * PageSize;
}
