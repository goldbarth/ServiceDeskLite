namespace ServiceDeskLite.Api.Http.ProblemDetails;

internal static class HttpContextAccessorHolder
{
    public static IHttpContextAccessor Accessor { get; private set; } = default!;

    public static HttpContext? HttpContext => Accessor.HttpContext;

    public static void Set(IHttpContextAccessor accessor) => Accessor = accessor;
}
