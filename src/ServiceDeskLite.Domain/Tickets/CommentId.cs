namespace ServiceDeskLite.Domain.Tickets;

public readonly record struct CommentId(Guid Value)
{
    public static CommentId New() => new(Guid.NewGuid());
}
