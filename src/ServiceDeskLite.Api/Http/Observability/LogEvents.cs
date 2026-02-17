namespace ServiceDeskLite.Api.Http.Observability;

public static class LogEvents
{
    // Stable EventIds for structured logging (Serilog/ILogger compatible)
    public static readonly EventId ApiError = new(1000, nameof(ApiError));
    public static readonly EventId ApiConflict = new(1001, nameof(ApiConflict));
    public static readonly EventId ApiValidation = new(1002, nameof(ApiValidation));
    public static readonly EventId ApiNotFound = new(1003, nameof(ApiNotFound));
}
