using ServiceDeskLite.Application.Common;

namespace ServiceDeskLite.Api.Http.ProblemDetails;

public static class ResultMappingExtensions
{
    public static IResult ToHttpResult<T>(
        this Result<T> result,
        HttpContext ctx,
        ResultToProblemDetailsMapper mapper,
        Func<T, IResult> onSuccess)
        => mapper.ToHttpResult(ctx, result, onSuccess);
}
