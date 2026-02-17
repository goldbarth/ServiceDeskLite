using Microsoft.AspNetCore.Diagnostics;

using ServiceDeskLite.Api.Http.Observability;
using ServiceDeskLite.Api.Http.ProblemDetails;
using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Api.Http.ExceptionHandling;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;
    private readonly ExceptionToApplicationErrorMapper _errorMapper;
    private readonly ApiProblemDetailsFactory _pdFactory;

    public ApiExceptionHandler(
        ILogger<ApiExceptionHandler> logger,
        ExceptionToApplicationErrorMapper errorMapper,
        ApiProblemDetailsFactory pdFactory)
    {
        _logger = logger;
        _errorMapper = errorMapper;
        _pdFactory = pdFactory;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (ExceptionClassification.IsCancellation(exception, httpContext))
            return false; // let pipeline handle aborted requests naturally

        var error = _errorMapper.Map(exception, httpContext);
        var (status, title) = MapToHttp(error);

        // Logging policy
        var traceId = Correlation.GetTraceId(httpContext);
        using (_logger.BeginScope(new Dictionary<string, object?>
               {
                   ["traceId"] = traceId,
                   ["code"] = error.Code,
                   ["errorType"] = error.Type.ToString()
               }))
        {
            if (status >= 500)
                _logger.LogError(exception, "Unhandled exception.");
            else if (status == StatusCodes.Status409Conflict)
                _logger.LogWarning(LogEvents.ApiConflict, exception, "Conflict.");
            else if (status == StatusCodes.Status400BadRequest)
                _logger.LogWarning(LogEvents.ApiValidation, exception, "Bad request.");
            else
                _logger.LogInformation(LogEvents.ApiError, exception, "Request failed.");
        }

        var pd = _pdFactory.Create(httpContext, status, title, error);

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(pd, cancellationToken);
        return true;
    }

    private static (int status, string title) MapToHttp(ApplicationError error)
        => error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, ApiProblemDetailsConventions.Titles.Validation),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, ApiProblemDetailsConventions.Titles.NotFound),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, ApiProblemDetailsConventions.Titles.Conflict),
            _ => (StatusCodes.Status500InternalServerError, ApiProblemDetailsConventions.Titles.Unexpected),
        };
}
