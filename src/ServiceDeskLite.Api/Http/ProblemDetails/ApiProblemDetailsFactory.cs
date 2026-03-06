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
        if (error.FieldErrors is { Count: > 0 })
            return CreateWithFieldErrors(ctx, title, error);

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

    private Microsoft.AspNetCore.Mvc.ValidationProblemDetails CreateWithFieldErrors(
        HttpContext ctx,
        string title,
        ApplicationError error)
    {
        var vp = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
            Detail = null,
            Instance = ctx.Request.Path,
        };

        foreach (var (field, messages) in error.FieldErrors!)
            vp.Errors[field] = messages;

        vp.Extensions[ProblemDetailsContract.Extensions.Code] = error.Code;
        vp.Extensions[ProblemDetailsContract.Extensions.ErrorType] = MapErrorType(error.Type);
        vp.Extensions[ProblemDetailsContract.Extensions.TraceId] = Correlation.GetTraceId(ctx);

        return vp;
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
