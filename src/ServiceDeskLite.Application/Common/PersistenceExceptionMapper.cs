namespace ServiceDeskLite.Application.Common;

public static class PersistenceExceptionMapper
{
    public static ApplicationError ToApplicationError(Exception ex)
    {
        if (IsConflict(ex))
            return ApplicationError.Conflict("persistence.conflict", ex.Message);

        return ApplicationError.Unexpected("persistence.unexpected", ex.Message);
    }

    private static bool IsConflict(Exception ex) =>
        ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("concurrency", StringComparison.OrdinalIgnoreCase);
}