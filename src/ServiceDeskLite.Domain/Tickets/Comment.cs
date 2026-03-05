using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Domain.Tickets;

public sealed class Comment
{
    public const int MaxContentLength = 2000;
    public const int MaxAuthorLength = 100;

    public CommentId Id { get; }
    public string Content { get; }
    public string? Author { get; }
    public DateTimeOffset CreatedAt { get; }

    public Comment(
        CommentId id,
        string content,
        DateTimeOffset createdAt,
        string? author = null)
    {
        Guard.NotNullOrWhiteSpace(content, nameof(content));
        Guard.MaxLength(content, MaxContentLength, nameof(content));

        if (author is not null)
            Guard.MaxLength(author, MaxAuthorLength, nameof(author));

        Id = id;
        Content = content;
        CreatedAt = createdAt;
        Author = author;
    }
}
