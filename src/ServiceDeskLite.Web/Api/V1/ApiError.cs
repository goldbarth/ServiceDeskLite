namespace ServiceDeskLite.Web.Api.V1;

public sealed class ApiError
{
    public int Status { get; init; }
    public string? Title { get; init; }
    public string? Detail { get; init; }

    public string? Code { get; init; }
    public string? ErrorType { get; init; }
    public string? TraceId { get; init; }

    public Dictionary<string, object>? Meta { get; init; }
}
