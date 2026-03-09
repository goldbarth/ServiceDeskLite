using Microsoft.AspNetCore.Diagnostics;

using ServiceDeskLite.Api.Http.ExceptionHandling;
using ServiceDeskLite.Api.Http.ProblemDetails;
using ServiceDeskLite.Application.Common;

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
        app.UseStatusCodePages(WriteEmptyBadRequestAsProblemDetailsAsync);
        return app;
    }

    private static Task WriteEmptyBadRequestAsProblemDetailsAsync(StatusCodeContext statusCodeContext)
    {
        var httpContext = statusCodeContext.HttpContext;
        var response = httpContext.Response;

        if (response.StatusCode != StatusCodes.Status400BadRequest)
            return Task.CompletedTask;

        if (response.HasStarted)
            return Task.CompletedTask;

        if (response.ContentLength is > 0)
            return Task.CompletedTask;

        if (!string.IsNullOrWhiteSpace(response.ContentType))
            return Task.CompletedTask;

        var factory = httpContext.RequestServices.GetRequiredService<ApiProblemDetailsFactory>();
        var error = ApplicationError.Validation(
            code: "api.request.bad_request",
            message: "Request payload or parameters are invalid.");

        var problem = factory.Create(
            httpContext,
            StatusCodes.Status400BadRequest,
            ApiProblemDetailsConventions.Titles.Validation,
            error);

        return Results.Problem(problem).ExecuteAsync(httpContext);
    }
}
