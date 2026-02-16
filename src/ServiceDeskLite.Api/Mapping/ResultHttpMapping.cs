using Microsoft.AspNetCore.Mvc;

using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Api.Mapping;

public static class ResultHttpMapping
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        return result.IsSuccess ? onSuccess(result.Value!) : ToProblem(result.Error!);
    }

    private static IResult ToProblem(ApplicationError error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation      => StatusCodes.Status400BadRequest,
            ErrorType.DomainViolation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound        => StatusCodes.Status404NotFound,
            ErrorType.Conflict        => StatusCodes.Status409Conflict,
            ErrorType.Unexpected      => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Detail = error.Message,
            Extensions =
            {
                ["code"] = error.Code,
                ["errorType"] = error.Type.ToString()
            },
            Title = error.Type switch
            {
                ErrorType.Validation      => "Validation failed.",
                ErrorType.DomainViolation => "Domain rule violated.",
                ErrorType.NotFound        => "Resource not found.",
                ErrorType.Conflict        => "Conflict.",
                _                         => "Unexpected error."
            }
        };

        if (error.Meta is not null && error.Meta.Count > 0)
            problem.Extensions["meta"] = error.Meta;
        
        return Results.Problem(problem);
    }
}
