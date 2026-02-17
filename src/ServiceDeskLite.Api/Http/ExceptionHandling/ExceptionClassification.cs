namespace ServiceDeskLite.Api.Http.ExceptionHandling;

public static class ExceptionClassification
{
    public static bool IsClientBadRequest(Exception ex)
        => ex is BadHttpRequestException 
            or FormatException 
            or InvalidOperationException; // keep narrow; expand only with evidence

    public static bool IsCancellation(Exception ex, HttpContext ctx)
        => ex is OperationCanceledException
           || (ctx.RequestAborted.IsCancellationRequested && ex is TaskCanceledException);
}
