namespace ServiceDeskLite.Application.Common;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    
    public ApplicationError? Error { get; }
    
    protected  Result(bool isSuccess, ApplicationError? error)
    {
        if (isSuccess && error is not null)
            throw new InvalidOperationException("Successful result cannot have an error.");
        
        if (!isSuccess && error is null)
            throw new InvalidOperationException("Failed result must have an error.");
        
        IsSuccess = isSuccess;
        Error = error;
    }
    
    public static Result Success() 
        => new(true, null);
    public static Result Failure(ApplicationError error) 
        => new(false, error);
    
    // Factories
    public static Result NotFound(
        string code, 
        string message,
        IReadOnlyDictionary<string, object>? meta = null)
        => Failure(ApplicationError.NotFound( code, message, meta));
    
    public static Result Validation(
        string code, 
        string message,
        IReadOnlyDictionary<string, object>? meta = null)
        => Failure(ApplicationError.Validation( code, message, meta));
    
    public static Result DomainViolation(
        string code, 
        string message,
        IReadOnlyDictionary<string, object>? meta = null)
        => Failure(ApplicationError.DomainViolation( code, message, meta));
}
