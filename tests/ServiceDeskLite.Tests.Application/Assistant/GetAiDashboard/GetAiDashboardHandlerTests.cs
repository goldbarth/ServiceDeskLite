using FluentAssertions;

using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Assistant.GetAiDashboard;
using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Tests.Application.Assistant.GetAiDashboard;

public sealed class GetAiDashboardHandlerTests
{
    [Fact]
    public async Task HandleAsync_NullQuery_ReturnsValidationFailure()
    {
        var handler = new GetAiDashboardHandler(new FakeAiDashboardRepository(), new FakeClock());

        var result = await handler.HandleAsync(null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("get_ai_dashboard.query.null");
    }

    [Fact]
    public async Task HandleAsync_PassesTheTrailingWindowToTheRepository()
    {
        var fixedNow = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);
        var repo = new FakeAiDashboardRepository();
        var handler = new GetAiDashboardHandler(repo, new FakeClock { UtcNow = fixedNow });

        await handler.HandleAsync(new GetAiDashboardQuery());

        repo.CapturedNow.Should().Be(fixedNow);
        repo.CapturedSince.Should().Be(fixedNow.AddDays(-GetAiDashboardHandler.WindowDays));
    }

    [Fact]
    public async Task HandleAsync_ReturnsRepositoryData()
    {
        var repo = new FakeAiDashboardRepository();
        var handler = new GetAiDashboardHandler(repo, new FakeClock());

        var result = await handler.HandleAsync(new GetAiDashboardQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value!.WindowDays.Should().Be(GetAiDashboardHandler.WindowDays);
    }

    private sealed class FakeAiDashboardRepository : IAiDashboardRepository
    {
        public DateTimeOffset? CapturedSince { get; private set; }
        public DateTimeOffset? CapturedNow { get; private set; }

        public Task<AiDashboardDto> GetAsync(
            DateTimeOffset since, DateTimeOffset now, CancellationToken ct = default)
        {
            CapturedSince = since;
            CapturedNow = now;

            return Task.FromResult(new AiDashboardDto(
                WindowDays: (int)Math.Round((now - since).TotalDays),
                Volume: new TicketVolumeDto(0, 0),
                Automation: new AutomationDto(0, 0),
                Retrieval: new RetrievalDto(0, 0, 0, null, false),
                Tools: [],
                Tokens: new TokenUsageDto(0, 0, 0)));
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
