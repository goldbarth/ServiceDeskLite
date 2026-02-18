using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Application.Tickets.Shared;

public readonly record struct Paging(int Page, int PageSize)
{
    public static Paging Default => new(Page: PagingPolicy.MinPage, PageSize: PagingPolicy.DefaultPageSize);
    
    public int Skip => (Page - 1) * PageSize;
}
