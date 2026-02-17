using ServiceDeskLite.Api.Http.Observability;
using ServiceDeskLite.Application.Common;

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
                [ApiProblemDetailsConventions.ExtensionKeys.Code] = error.Code,
                [ApiProblemDetailsConventions.ExtensionKeys.ErrorType] = error.Type.ToString(),
                [ApiProblemDetailsConventions.ExtensionKeys.TraceId] = Correlation.GetTraceId(ctx)
            }
        };

        if (error.Meta is not null && error.Meta.Count > 0)
            pd.Extensions[ApiProblemDetailsConventions.ExtensionKeys.Meta] = error.Meta;

        return pd;
    }
}
