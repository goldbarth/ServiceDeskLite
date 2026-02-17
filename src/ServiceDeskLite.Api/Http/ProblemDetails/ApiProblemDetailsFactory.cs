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
                [ContractsProblemDetailsConventions.ExtensionKeys.Code] = error.Code,
                [ContractsProblemDetailsConventions.ExtensionKeys.ErrorType] = error.Type.ToString(),
                [ContractsProblemDetailsConventions.ExtensionKeys.TraceId] = Correlation.GetTraceId(ctx)
            }
        };

        if (error.Meta is not null && error.Meta.Count > 0)
            pd.Extensions[ContractsProblemDetailsConventions.ExtensionKeys.Meta] = error.Meta;

        return pd;
    }
}
