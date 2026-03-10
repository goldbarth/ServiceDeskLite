using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Application.Tickets.GetDashboardSummary;

namespace ServiceDeskLite.Tests.Application.Tickets.GetDashboardSummary;

public class GetDashboardSummaryHandlerTests
{
    [Fact]
    public async Task HandleAsync_NullQuery_ReturnsValidationFailure()
    {
        var handler = new GetDashboardSummaryHandler(new FakeDashboardRepository(), new FakeClock());

        var result = await handler.HandleAsync(null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("get_dashboard_summary.query.null");
    }

    [Fact]
    public async Task HandleAsync_ValidQuery_ReturnsRepositoryData()
    {
        var expected = new DashboardSummaryDto(
            NewCount: 3,
            TriagedCount: 2,
            InProgressCount: 1,
            OverdueCount: 4,
            ResolvedLast7DaysCount: 5);
        var repo = new FakeDashboardRepository(expected);
        var handler = new GetDashboardSummaryHandler(repo, new FakeClock());

        var result = await handler.HandleAsync(new GetDashboardSummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public async Task HandleAsync_ValidQuery_PassesClockTimeToRepository()
    {
        var fixedNow = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock { UtcNow = fixedNow };
        var repo = new FakeDashboardRepository();
        var handler = new GetDashboardSummaryHandler(repo, clock);

        await handler.HandleAsync(new GetDashboardSummaryQuery());

        repo.CapturedNow.Should().Be(fixedNow);
    }

    // ── Fake ──────────────────────────────────────────────────────────────────

    private sealed class FakeDashboardRepository(DashboardSummaryDto? result = null) : IDashboardRepository
    {
        private static readonly DashboardSummaryDto _empty = new(0, 0, 0, 0, 0);

        public DateTimeOffset? CapturedNow { get; private set; }

        public Task<DashboardSummaryDto> GetSummaryAsync(DateTimeOffset now, CancellationToken ct = default)
        {
            CapturedNow = now;
            return Task.FromResult(result ?? _empty);
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
