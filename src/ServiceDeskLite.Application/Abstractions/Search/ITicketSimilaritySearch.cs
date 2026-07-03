using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Abstractions.Search;

/// <summary>
/// Semantic (embedding-based) ticket search. Implemented by the Postgres
/// provider via pgvector; other providers report IsAvailable=false so callers
/// (e.g. the assistant tool) can degrade gracefully instead of pretending
/// "no matches".
/// </summary>
public interface ITicketSimilaritySearch
{
    Task<TicketSimilaritySearchResult> SearchAsync(string query, int limit, CancellationToken ct);
}

public sealed record TicketSimilarityMatch(
    TicketId Id,
    string Title,
    TicketStatus Status,
    TicketPriority Priority,
    double Similarity);

public sealed record TicketSimilaritySearchResult(
    bool IsAvailable,
    IReadOnlyList<TicketSimilarityMatch> Matches)
{
    public static TicketSimilaritySearchResult Unavailable { get; } = new(false, []);
}
