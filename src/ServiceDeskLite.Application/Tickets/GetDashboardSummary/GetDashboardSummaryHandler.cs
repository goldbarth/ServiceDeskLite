using ServiceDeskLite.Application.Abstractions.Persistence;
using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Application.Tickets.GetDashboardSummary;

public sealed class GetDashboardSummaryHandler
{
    private readonly IDashboardRepository _repository;

    public GetDashboardSummaryHandler(IDashboardRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<Result<DashboardSummaryDto>> HandleAsync(
        GetDashboardSummaryQuery? query,
        CancellationToken ct = default)
    {
        if (query is null)
            return Result<DashboardSummaryDto>.Validation(
                "get_dashboard_summary.query.null",
                "Query must not be null.");

        var now = DateTimeOffset.UtcNow;
        var summary = await _repository.GetSummaryAsync(now, ct);

        return Result<DashboardSummaryDto>.Success(summary);
    }
}