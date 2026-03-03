using ServiceDeskLite.Api.Http.Observability;
using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Contracts.V1.Common;

namespace ServiceDeskLite.Api.Http.ProblemDetails;

public sealed class ApiProblemDetailsFactory
{
    public Microsoft.AspNetCore.Mvc.ProblemDetails Create(
        HttpContext ctx,
        int status,
        string title,
        ApplicationError error)
    {
        var pd = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = status,
            Title = title,
            // Production: keep Detail empty
            Detail = null,
            Instance = ctx.Request.Path,
            Extensions =
            {
                [ProblemDetailsContract.Extensions.Code] = error.Code,
                [ProblemDetailsContract.Extensions.ErrorType] = MapErrorType(error.Type),
                [ProblemDetailsContract.Extensions.TraceId] = Correlation.GetTraceId(ctx)
            }
        };

        if (error.Meta is not null && error.Meta.Count > 0)
            pd.Extensions[ProblemDetailsContract.Extensions.Meta] = error.Meta;

        return pd;
    }

    private static string MapErrorType(ErrorType type) => type switch
    {
        ErrorType.Validation => ProblemDetailsContract.ErrorTypes.Validation,
        ErrorType.NotFound => ProblemDetailsContract.ErrorTypes.NotFound,
        ErrorType.Conflict => ProblemDetailsContract.ErrorTypes.Conflict,
        ErrorType.DomainViolation => ProblemDetailsContract.ErrorTypes.DomainViolation,
        ErrorType.Unexpected => ProblemDetailsContract.ErrorTypes.Unexpected,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}
