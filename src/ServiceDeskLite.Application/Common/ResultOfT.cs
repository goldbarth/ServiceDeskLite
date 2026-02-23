namespace ServiceDeskLite.Application.Common;

public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    
    public T? Value => IsSuccess  
        ? field 
        : throw new InvalidOperationException("Cannot access Value of a failed result.");
    
    public ApplicationError? Error { get; }

    private Result(bool isSuccess, T? value, ApplicationError? error)
    {
        if (isSuccess && error is not null)
            throw new InvalidOperationException("Successful result cannot have an error.");
        
        if (!isSuccess && error is null)
            throw new InvalidOperationException("Failed result must have an error.");
        
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }
    
    public static Result<T> Success(T value)
        => new(true, value, null);
    
    public static Result<T> Failure(ApplicationError error)
        => new(false, default, error);
    
    public static Result<T> NotFound(
        string code,
        string message,
        IReadOnlyDictionary<string, object>? meta = null)
        => Failure(ApplicationError.NotFound(code, message, meta));

    public static Result<T> Validation(
        string code,
        string message,
        IReadOnlyDictionary<string, object>? meta = null)
        => Failure(ApplicationError.Validation(code, message, meta));

    public static Result<T> DomainViolation(
        string code,
        string message,
        IReadOnlyDictionary<string, object>? meta = null)
        => Failure(ApplicationError.DomainViolation(code, message, meta));

    public static Result<T> Conflict(
        string code,
        string message,
        IReadOnlyDictionary<string, object>? meta = null)
        => Failure(ApplicationError.Conflict(code, message, meta));
}
