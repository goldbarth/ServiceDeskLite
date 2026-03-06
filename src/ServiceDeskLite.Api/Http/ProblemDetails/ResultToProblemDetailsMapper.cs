using ServiceDeskLite.Api.Http.Observability;
using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Api.Http.ProblemDetails;

public sealed class ResultToProblemDetailsMapper
{
    private readonly ApiProblemDetailsFactory _factory;
    private readonly ILogger<ResultToProblemDetailsMapper> _logger;

    public ResultToProblemDetailsMapper(
        ApiProblemDetailsFactory factory,
        ILogger<ResultToProblemDetailsMapper> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public IResult ToHttpResult<T>(HttpContext ctx, Result<T> result, Func<T, IResult> onSuccess)
        => result.IsSuccess
            ? onSuccess(result.Value!)
            : ToProblem(ctx, result.Error!);

    public IResult ToProblem(HttpContext ctx, ApplicationError error)
    {
        var (status, title) = MapToHttp(error);

        Log(ctx, error, status);

        // Production policy: Detail empty (Factory sets Detail=null)
        var pd = _factory.Create(ctx, status, title, error);
        return Results.Problem(pd);
    }

    private void Log(HttpContext ctx, ApplicationError error, int status)
    {
        var traceId = Correlation.GetTraceId(ctx);

        using (_logger.BeginScope(new Dictionary<string, object?>
               {
                   ["TraceId"]   = traceId,
                   ["ErrorCode"] = error.Code,
                   ["ErrorType"] = error.Type.ToString()
               }))
        {
            if (status >= 500)
                _logger.LogError("Request failed [{TraceId}]: {ErrorCode}", traceId, error.Code);
            else if (status == StatusCodes.Status409Conflict)
                _logger.LogWarning(LogEvents.ApiConflict, "Conflict [{TraceId}]: {ErrorCode}", traceId, error.Code);
            else if (status == StatusCodes.Status400BadRequest)
                _logger.LogWarning(LogEvents.ApiValidation, "Validation failed [{TraceId}]: {ErrorCode}", traceId, error.Code);
            else if (status == StatusCodes.Status404NotFound)
                _logger.LogInformation(LogEvents.ApiNotFound, "Not found [{TraceId}]: {ErrorCode}", traceId, error.Code);
            else
                _logger.LogInformation(LogEvents.ApiError, "Request failed [{TraceId}]: {ErrorCode}", traceId, error.Code);
        }
    }

    private static (int status, string title) MapToHttp(ApplicationError error)
        => error.Type switch
        {
            ErrorType.Validation      => (StatusCodes.Status400BadRequest, ApiProblemDetailsConventions.Titles.Validation),
            ErrorType.DomainViolation => (StatusCodes.Status400BadRequest, ApiProblemDetailsConventions.Titles.Validation), // bewusst: 400
            ErrorType.NotFound        => (StatusCodes.Status404NotFound, ApiProblemDetailsConventions.Titles.NotFound),
            ErrorType.Conflict        => (StatusCodes.Status409Conflict, ApiProblemDetailsConventions.Titles.Conflict),
            _                         => (StatusCodes.Status500InternalServerError, ApiProblemDetailsConventions.Titles.Unexpected),
        };
}
