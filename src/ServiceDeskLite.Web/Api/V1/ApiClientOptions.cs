namespace ServiceDeskLite.Web.Api.V1;

public sealed class ApiClientOptions
{
    public string BaseUrl { get; init; } = default!;
    public int TimeoutSeconds { get; init; } = 10;
}
