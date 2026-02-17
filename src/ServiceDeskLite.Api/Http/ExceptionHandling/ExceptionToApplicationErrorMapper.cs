using ServiceDeskLite.Application.Common;
using ServiceDeskLite.Domain.Common;

using static ServiceDeskLite.Application.Common.ApplicationError;

namespace ServiceDeskLite.Api.Http.ExceptionHandling;

public sealed class ExceptionToApplicationErrorMapper
{
    public ApplicationError Map(Exception ex, HttpContext ctx)
    {
        // Domain fallback (should be rare, but supported)
        if (ex is DomainException domainEx)
            return DomainExceptionMapper.ToApplicationError(domainEx);

        // Binding / JSON / malformed input
        if (ExceptionClassification.IsClientBadRequest(ex))
        {
            return Validation(
                code: "api.request.bad_request",
                message: "Request payload or parameters are invalid.",
                meta: null);
        }

        // Default
        return Unexpected(
            code: "api.unexpected",
            message: "An unexpected error occurred.",
            meta: null);
    }
}
