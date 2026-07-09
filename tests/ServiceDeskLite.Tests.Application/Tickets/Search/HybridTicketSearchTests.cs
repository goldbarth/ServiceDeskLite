using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Abstractions.Search;
using ServiceDeskLite.Application.Tickets.Search;
using ServiceDeskLite.Application.Tickets.Shared;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Application.Tickets.Search;

public sealed class HybridTicketSearchTests
{
    private static readonly TicketId A = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
    private static readonly TicketId B = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"));
    private static readonly TicketId C = new(Guid.Parse("cccccccc-0000-0000-0000-000000000003"));

    private static TicketSimilarityMatch Sem(TicketId id, double sim, TicketPriority p = TicketPriority.Medium) =>
        new(id, $"T-{id.Value.ToString()[..4]}", TicketStatus.New, p, sim);

    private static TicketListItemDto Kw(TicketId id, TicketPriority p = TicketPriority.Medium) =>
        new(id, $"T-{id.Value.ToString()[..4]}", TicketStatus.New, p, TicketCategory.Uncategorized, DateTimeOffset.UtcNow, null, null, [], false, "#ABC123");

    private static HybridTicketSearchQuery Query(
        int limit = 10,
        IReadOnlyCollection<TicketStatus>? statuses = null,
        IReadOnlyCollection<TicketPriority>? priorities = null) =>
        new("anything", limit, statuses, priorities);

    [Fact]
    public async Task Blends_signals_and_ranks_shared_ticket_first()
    {
        // A only semantic, B in both, C only keyword → B should win the fusion.
        var search = new HybridTicketSearch(
            new FakeSemantic(available: true, [Sem(A, 0.9), Sem(B, 0.8)]),
            new FakeRepo([Kw(B), Kw(C)]));

        var result = await search.SearchAsync(Query(), CancellationToken.None);

        result.SemanticAvailable.Should().BeTrue();
        result.Matches[0].Id.Should().Be(B);
        result.Matches[0].FromSemantic.Should().BeTrue();
        result.Matches[0].FromKeyword.Should().BeTrue();
        result.Matches.Select(m => m.Id).Should().Contain([A, B, C]);
    }

    [Fact]
    public async Task Top_match_relevance_is_normalized_to_one()
    {
        var search = new HybridTicketSearch(
            new FakeSemantic(available: true, [Sem(A, 0.9)]),
            new FakeRepo([Kw(A), Kw(B)]));

        var result = await search.SearchAsync(Query(), CancellationToken.None);

        result.Matches.Max(m => m.Relevance).Should().Be(1.0);
    }

    [Fact]
    public async Task Semantic_unavailable_degrades_to_keyword_only()
    {
        var search = new HybridTicketSearch(
            new FakeSemantic(available: false, []),
            new FakeRepo([Kw(A), Kw(B)]));

        var result = await search.SearchAsync(Query(), CancellationToken.None);

        result.SemanticAvailable.Should().BeFalse();
        result.Matches.Should().OnlyContain(m => !m.FromSemantic && m.FromKeyword);
        result.Matches.Select(m => m.Id).Should().BeEquivalentTo([A, B]);
    }

    [Fact]
    public async Task Priority_filter_constrains_semantic_matches()
    {
        // Semantic returns a High and a Low ticket; keyword empty. Filtering to High
        // must drop the Low one (the service post-filters semantic, which has no filter).
        var search = new HybridTicketSearch(
            new FakeSemantic(available: true, [Sem(A, 0.9, TicketPriority.High), Sem(B, 0.8, TicketPriority.Low)]),
            new FakeRepo([]));

        var result = await search.SearchAsync(
            Query(priorities: [TicketPriority.High]), CancellationToken.None);

        result.Matches.Select(m => m.Id).Should().ContainSingle().Which.Should().Be(A);
    }

    [Fact]
    public async Task Priority_breaks_ties_in_favour_of_higher_priority()
    {
        // Symmetric ranks (semantic [A,B], keyword [B,A]) give equal RRF; the priority
        // nudge must then rank the Critical ticket above the Low one.
        var search = new HybridTicketSearch(
            new FakeSemantic(available: true, [Sem(A, 0.9, TicketPriority.Critical), Sem(B, 0.9, TicketPriority.Low)]),
            new FakeRepo([Kw(B, TicketPriority.Low), Kw(A, TicketPriority.Critical)]));

        var result = await search.SearchAsync(Query(), CancellationToken.None);

        result.Matches[0].Id.Should().Be(A, "Critical outranks Low on an otherwise even fusion");
    }

    [Fact]
    public async Task Respects_limit()
    {
        var search = new HybridTicketSearch(
            new FakeSemantic(available: true, [Sem(A, 0.9), Sem(B, 0.8), Sem(C, 0.7)]),
            new FakeRepo([]));

        var result = await search.SearchAsync(Query(limit: 2), CancellationToken.None);

        result.Matches.Should().HaveCount(2);
    }

    private sealed class FakeSemantic(bool available, IReadOnlyList<TicketSimilarityMatch> matches)
        : ITicketSimilaritySearch
    {
        public Task<TicketSimilaritySearchResult> SearchAsync(string query, int limit, CancellationToken ct) =>
            Task.FromResult(available
                ? new TicketSimilaritySearchResult(true, matches)
                : TicketSimilaritySearchResult.Unavailable);
    }

    private sealed class FakeRepo(IReadOnlyList<TicketListItemDto> items) : ITicketRepository
    {
        public Task<PagedResult<TicketListItemDto>> SearchAsync(
            TicketSearchCriteria criteria, Paging paging, SortSpec sort, CancellationToken ct = default)
        {
            // Mimic the repo's metadata filtering so the keyword signal honors constraints.
            IEnumerable<TicketListItemDto> q = items;
            if (criteria.Statuses is { Count: > 0 })
                q = q.Where(t => criteria.Statuses.Contains(t.Status));
            if (criteria.Priorities is { Count: > 0 })
                q = q.Where(t => criteria.Priorities.Contains(t.Priority));

            return Task.FromResult(new PagedResult<TicketListItemDto>(q.ToList(), items.Count, paging));
        }

        public Task AddAsync(Ticket ticket, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Ticket?> GetByIdAsync(TicketId id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(TicketId id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
