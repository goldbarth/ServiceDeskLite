namespace ServiceDeskLite.Contracts.V1.Tickets;

public sealed record CommentResponse(
    Guid Id,
    string Content,
    string? Author,
    DateTimeOffset CreatedAt);
