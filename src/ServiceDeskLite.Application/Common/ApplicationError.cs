namespace ServiceDeskLite.Application.Common;

public sealed record ApplicationError(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, object>? Meta = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static ApplicationError Validation(string code, string message,
        IReadOnlyDictionary<string, object>? meta = null)
        => new(code, message, ErrorType.Validation, meta);

    public static ApplicationError ValidationWithFields(string code, string message,
        IReadOnlyDictionary<string, string[]> fieldErrors)
        => new(code, message, ErrorType.Validation, Meta: null, FieldErrors: fieldErrors);
    
    public static ApplicationError NotFound(string code, string message,
        IReadOnlyDictionary<string, object>? meta = null)
        => new(code, message, ErrorType.NotFound, meta);
    
    public static ApplicationError Conflict(string code, string message,
        IReadOnlyDictionary<string, object>? meta = null)
        => new(code, message, ErrorType.Conflict, meta);
    
    public static ApplicationError DomainViolation(string code, string message,
        IReadOnlyDictionary<string, object>? meta = null)
        => new(code, message, ErrorType.DomainViolation, meta);
    
    public static ApplicationError Unexpected(string code, string message,
        IReadOnlyDictionary<string, object>? meta = null)
        => new(code, message, ErrorType.Unexpected, meta);
}
