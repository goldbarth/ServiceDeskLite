using ServiceDeskLite.Api.Http.ExceptionHandling;
using ServiceDeskLite.Api.Http.ProblemDetails;

namespace ServiceDeskLite.Api.Composition;

public static class ApiErrorHandlingExtensions
{
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(); // uses ProblemDetails types; safe in API layer
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddSingleton<ApiProblemDetailsFactory>();
        services.AddSingleton<ExceptionToApplicationErrorMapper>();

        // ResultToProblemDetailsMapper registration (move your existing mapper)
        services.AddSingleton<ResultToProblemDetailsMapper>();

        return services;
    }

    public static IApplicationBuilder UseApiErrorHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(); // integrates IExceptionHandler
        return app;
    }
}
