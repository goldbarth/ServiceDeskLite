using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Application.Tickets.GetDashboardSummary;

public sealed class GetDashboardSummaryHandler
{
    private readonly IDashboardRepository _repository;
    private readonly IClock _clock;

    public GetDashboardSummaryHandler(IDashboardRepository repository, IClock clock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<DashboardSummaryDto>> HandleAsync(
        GetDashboardSummaryQuery? query,
        CancellationToken ct = default)
    {
        if (query is null)
            return Result<DashboardSummaryDto>.Validation(
                "get_dashboard_summary.query.null",
                "Query must not be null.");

        var summary = await _repository.GetSummaryAsync(_clock.UtcNow, ct);

        return Result<DashboardSummaryDto>.Success(summary);
    }
}