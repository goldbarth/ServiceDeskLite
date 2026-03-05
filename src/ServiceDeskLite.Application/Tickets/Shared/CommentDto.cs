using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.Shared;

public sealed record CommentDto(
    CommentId Id,
    string Content,
    string? Author,
    DateTimeOffset CreatedAt);
