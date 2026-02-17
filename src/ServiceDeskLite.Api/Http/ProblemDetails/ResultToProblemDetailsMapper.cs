using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Api.Http.ProblemDetails;

public sealed class ResultToProblemDetailsMapper
{
    private readonly ApiProblemDetailsFactory _factory;

    public ResultToProblemDetailsMapper(ApiProblemDetailsFactory factory)
        => _factory = factory;

    public IResult ToHttpResult<T>(HttpContext ctx, Result<T> result, Func<T, IResult> onSuccess)
        => result.IsSuccess
            ? onSuccess(result.Value!)
            : ToProblem(ctx, result.Error!);

    public IResult ToProblem(HttpContext ctx, ApplicationError error)
    {
        var (status, title) = MapToHttp(error);

        // Production policy: Detail empty (Factory sets Detail=null)
        var pd = _factory.Create(ctx, status, title, error);
        return Results.Problem(pd);
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
