using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Abstractions.Search;

/// <summary>
/// Ticket retrieval that blends semantic (embedding) and keyword signals into a
/// single ranked list, constrained by metadata (status, priority) and scored by a
/// documented fusion function (Reciprocal Rank Fusion + priority weighting). When
/// the semantic signal is unavailable (no Voyage key / InMemory), it degrades to a
/// keyword-only ranking and reports that honestly via
/// <see cref="HybridTicketSearchResult.SemanticAvailable"/> rather than pretending a
/// blended result.
/// </summary>
public interface IHybridTicketSearch
{
    Task<HybridTicketSearchResult> SearchAsync(HybridTicketSearchQuery query, CancellationToken ct);
}

/// <summary>
/// Retrieval request. <see cref="Statuses"/> and <see cref="Priorities"/> are the
/// metadata filters that constrain both signals; empty/null means "no constraint".
/// (The domain has no ticket "type"; priority is the categorical dimension the
/// roadmap's "type" maps to — see ADR-0030.)
/// </summary>
public sealed record HybridTicketSearchQuery(
    string Text,
    int Limit,
    IReadOnlyCollection<TicketStatus>? Statuses = null,
    IReadOnlyCollection<TicketPriority>? Priorities = null);

/// <summary>
/// One fused result. <see cref="Relevance"/> is the normalized fusion score (0..1,
/// top match = 1) — a relative confidence. <see cref="Similarity"/> is the raw
/// cosine similarity when the semantic signal matched this ticket, else null. The
/// <see cref="FromSemantic"/>/<see cref="FromKeyword"/> flags record which signals
/// contributed, so callers can be honest about the evidence.
/// </summary>
public sealed record HybridTicketMatch(
    TicketId Id,
    string Title,
    TicketStatus Status,
    TicketPriority Priority,
    double Relevance,
    double? Similarity,
    bool FromSemantic,
    bool FromKeyword);

public sealed record HybridTicketSearchResult(
    bool SemanticAvailable,
    IReadOnlyList<HybridTicketMatch> Matches)
{
    public static HybridTicketSearchResult Empty { get; } = new(false, []);
}
