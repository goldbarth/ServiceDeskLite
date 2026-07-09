using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Application.Assistant.GetAiDashboard;

public sealed class GetAiDashboardHandler
{
    /// <summary>
    /// Trailing window for every rate on the dashboard. Seven days to match the ticket
    /// dashboard's "resolved (7d)", so the two pages describe the same stretch of time.
    /// </summary>
    public const int WindowDays = 7;

    private readonly IAiDashboardRepository _repository;
    private readonly IClock _clock;

    public GetAiDashboardHandler(IAiDashboardRepository repository, IClock clock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<AiDashboardDto>> HandleAsync(
        GetAiDashboardQuery? query,
        CancellationToken ct = default)
    {
        if (query is null)
            return Result<AiDashboardDto>.Validation(
                "get_ai_dashboard.query.null",
                "Query must not be null.");

        var now = _clock.UtcNow;
        var metrics = await _repository.GetAsync(now.AddDays(-WindowDays), now, ct);

        return Result<AiDashboardDto>.Success(metrics);
    }
}
