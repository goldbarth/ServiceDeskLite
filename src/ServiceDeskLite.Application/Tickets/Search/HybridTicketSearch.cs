using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Application.Tickets.Search;

/// <summary>
/// Blends two retrieval signals for tickets — semantic (pgvector cosine, via
/// <see cref="ITicketSimilaritySearch"/>) and keyword (substring match, via
/// <see cref="ITicketRepository"/>) — into one ranking with Reciprocal Rank Fusion,
/// then nudges by priority. Metadata filters (status, priority) constrain both
/// signals. Lives in Application: fusion is provider-agnostic ranking logic composed
/// from ports, so both persistence providers share it and it is unit-testable with
/// fakes. When the semantic signal is unavailable (no Voyage key / InMemory), the
/// blend collapses to keyword-only and says so, instead of faking semantic evidence.
/// </summary>
public sealed class HybridTicketSearch : IHybridTicketSearch
{
    // Both signals weighted equally: neither cosine nor keyword recency is reliably
    // "better" here, and RRF already damps weak ranks. Tunable, documented in ADR-0030.
    private const double SemanticWeight = 1.0;
    private const double KeywordWeight = 1.0;

    // Priority is a light tie-breaker, not a dominant term: a strong textual match on a
    // low-priority ticket must still outrank a weak match on a critical one.
    private const double PriorityWeight = 0.15;

    private readonly ITicketSimilaritySearch _semantic;
    private readonly ITicketRepository _repository;

    public HybridTicketSearch(ITicketSimilaritySearch semantic, ITicketRepository repository)
    {
        _semantic = semantic ?? throw new ArgumentNullException(nameof(semantic));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<HybridTicketSearchResult> SearchAsync(HybridTicketSearchQuery query, CancellationToken ct)
    {
        // Over-fetch candidates from each signal so metadata filtering + fusion have room
        // to work before the final trim; cheap at this data size.
        var candidates = Math.Clamp(query.Limit * 5, 25, 100);

        var semantic = await SemanticCandidatesAsync(query, candidates, ct);
        var keyword = await KeywordCandidatesAsync(query, candidates, ct);

        var fused = ReciprocalRankFusion.Fuse(
        [
            ((IReadOnlyList<TicketId>)semantic.Candidates.Select(c => c.Id).ToList(), SemanticWeight),
            ((IReadOnlyList<TicketId>)keyword.Select(c => c.Id).ToList(), KeywordWeight),
        ]);

        if (fused.Count == 0)
            return new HybridTicketSearchResult(semantic.IsAvailable, []);

        var byId = new Dictionary<TicketId, Candidate>();
        foreach (var c in keyword.Concat(semantic.Candidates))
            byId.TryAdd(c.Id, c);

        // Apply the priority nudge, then normalize to the strongest so relevance reads as 0..1.
        var boosted = fused.ToDictionary(
            kv => kv.Key,
            kv => kv.Value * (1.0 + PriorityWeight * PriorityFactor(byId[kv.Key].Priority)));
        var max = boosted.Values.Max();

        var semanticIds = semantic.Candidates.Select(c => c.Id).ToHashSet();
        var keywordIds = keyword.Select(c => c.Id).ToHashSet();
        var similarityById = semantic.Candidates.ToDictionary(c => c.Id, c => c.Similarity);

        var matches = boosted
            .OrderByDescending(kv => kv.Value)
            .Take(query.Limit)
            .Select(kv =>
            {
                var c = byId[kv.Key];
                return new HybridTicketMatch(
                    c.Id, c.Title, c.Status, c.Priority,
                    Relevance: ReciprocalRankFusion.Normalize(kv.Value, max),
                    Similarity: similarityById.TryGetValue(c.Id, out var s) ? s : null,
                    FromSemantic: semanticIds.Contains(c.Id),
                    FromKeyword: keywordIds.Contains(c.Id));
            })
            .ToList();

        return new HybridTicketSearchResult(semantic.IsAvailable, matches);
    }

    private async Task<SemanticCandidates> SemanticCandidatesAsync(
        HybridTicketSearchQuery query, int candidates, CancellationToken ct)
    {
        var result = await _semantic.SearchAsync(query.Text, candidates, ct);
        if (!result.IsAvailable)
            return new SemanticCandidates(false, []);

        // Semantic search has no metadata filter, so constrain its matches here.
        var filtered = result.Matches
            .Where(m => Passes(m.Status, m.Priority, query))
            .Select(m => new Candidate(m.Id, m.Title, m.Status, m.Priority, m.Similarity))
            .ToList();

        return new SemanticCandidates(true, filtered);
    }

    private async Task<IReadOnlyList<Candidate>> KeywordCandidatesAsync(
        HybridTicketSearchQuery query, int candidates, CancellationToken ct)
    {
        var criteria = new TicketSearchCriteria(
            Text: query.Text,
            Statuses: query.Statuses,
            Priorities: query.Priorities);

        var page = await _repository.SearchAsync(
            criteria, new Paging(PagingPolicy.MinPage, candidates), SortSpec.Default, ct);

        return page.Items
            .Select(t => new Candidate(t.Id, t.Title, t.Status, t.Priority, null))
            .ToList();
    }

    private static bool Passes(TicketStatus status, TicketPriority priority, HybridTicketSearchQuery query) =>
        (query.Statuses is not { Count: > 0 } || query.Statuses.Contains(status))
        && (query.Priorities is not { Count: > 0 } || query.Priorities.Contains(priority));

    /// <summary>Maps priority to a 0..1 weight (Low = 0, Critical = 1) for the ranking nudge.</summary>
    private static double PriorityFactor(TicketPriority priority) => priority switch
    {
        TicketPriority.Low => 0.0,
        TicketPriority.Medium => 1.0 / 3.0,
        TicketPriority.High => 2.0 / 3.0,
        TicketPriority.Critical => 1.0,
        _ => 0.0,
    };

    private sealed record Candidate(
        TicketId Id, string Title, TicketStatus Status, TicketPriority Priority, double? Similarity);

    private sealed record SemanticCandidates(bool IsAvailable, IReadOnlyList<Candidate> Candidates);
}
