using ServiceDeskLite.Domain.Common;

namespace ServiceDeskLite.Application.Common;

public static class DomainExceptionMapper
{
    public static ApplicationError ToApplicationError(DomainException ex)
        => ApplicationError.DomainViolation(ex.Error.Code, ex.Error.Message);
}
