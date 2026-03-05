namespace ServiceDeskLite.Contracts.V1.Tickets;

public sealed record AddCommentRequest(string Content, string? Author);
