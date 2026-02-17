namespace ServiceDeskLite.Web.Api.V1;

public sealed class ApiResult<T>
{
    public bool IsSuccess => Error is null;
    public T? Value { get;}
    public ApiError? Error { get; }
    
    private ApiResult(T? value, ApiError? error)
    {
        Value = value;
        Error = error;
    }
    
    public static ApiResult<T> Success(T value)
        => new(value, null);
    
    public static ApiResult<T> Failure(ApiError? error)
        => new(default,  error);
}
