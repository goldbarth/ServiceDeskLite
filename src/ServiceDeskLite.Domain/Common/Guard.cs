namespace ServiceDeskLite.Domain.Common;

public static class Guard
{
    public static void NotNullOrWhiteSpace(string? value, string paramName)
    {
        if (!string.IsNullOrWhiteSpace(value)) return;
        throw new DomainException(new DomainError(
            "guard.not_empty",
            $"{paramName} must not be empty."));
    }

    public static void MaxLength(string value, int maxLength, string paramName)
    {
        if (value.Length <= maxLength) return;
        throw new DomainException(new DomainError(
            "guard.max_length",
            $"{paramName} must not exceed {maxLength} characters."));
    }

    public static void NotNull<T>(T? value, string paramName) where T : class
    {
        if (value is not null) return;
        throw new DomainException(new DomainError(
            "guard.not_null",
            $"{paramName} must not be null."));
    }
}
