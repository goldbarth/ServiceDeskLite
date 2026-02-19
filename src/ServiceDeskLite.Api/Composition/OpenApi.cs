using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace ServiceDeskLite.Api.Composition;

public static class OpenApi
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer<StringEnumSchemaTransformer>();
        });
        return services;
    }

    public static WebApplication UseApiDocumentation(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        return app;
    }
}

file sealed class StringEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!context.JsonTypeInfo.Type.IsEnum)
            return Task.CompletedTask;

        schema.Type = JsonSchemaType.String;
        schema.Format = null;
        schema.Enum = Enum.GetNames(context.JsonTypeInfo.Type)
            .Select(name => (JsonNode)JsonValue.Create(name)!)
            .ToList();

        return Task.CompletedTask;
    }
}
