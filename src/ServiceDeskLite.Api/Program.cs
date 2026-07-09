using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

using Serilog;

using ServiceDeskLite.Api.Composition;
using ServiceDeskLite.Api.Endpoints;
using ServiceDeskLite.Application.Agents.Seeding;
using ServiceDeskLite.Application.DependencyInjection;
using ServiceDeskLite.Application.Tickets.Seeding;
using ServiceDeskLite.Infrastructure.Persistence;

// ──────────── Logging ────────────

const string outputTemplate =
    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {TraceId}{NewLine}{Exception}";

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: outputTemplate)
    .CreateLogger();

// ──────────── Builder ────────────

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(outputTemplate: outputTemplate);
});

// ──────────── Services ────────────

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddApiDocumentation() // OpenAPI
    .AddApiErrorHandling() // ErrorHandling + ProblemDetails + Mapper
    .AddApplication() // Application Layer
    .AddApiInfrastructure(builder.Configuration) // Infrastructure Provider Switch
    .AddAssistant(builder.Configuration) // Anthropic client + tool-calling chat service
    // Last: it decorates the metrics sink the persistence provider registered above.
    .AddObservability(builder.Configuration); // Meter + ActivitySource, Prometheus / OTLP exporters

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebFrontend",
        p => p
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "ServiceDeskLite API", Version = "v1" });
    o.OrderActionsBy(api => $"{api.RelativePath}_{api.HttpMethod}");
});


var app = builder.Build();

// ──────────── Database Migration ────────────

if (app.Configuration["Persistence:Provider"] == "Postgres")
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider
        .GetRequiredService<ServiceDeskLiteDbContext>()
        .Database.MigrateAsync();
}

// ─────────── Middleware ───────────

app.UseApiRequestLogging();

app.UseApiDocumentation();

app.UseApiErrorHandling();
app.UseApiSecurity();
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.RoutePrefix = "swagger";
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "ServiceDeskLite API v1");
        o.DocumentTitle = "ServiceDeskLite Swagger";
    });

    // goldbarth: seeder for testing purpose
    using var scope = app.Services.CreateScope();
    // Agents first: tickets may reference them once assignment is wired up.
    var agentSeeder = scope.ServiceProvider.GetRequiredService<IAgentSeeder>();
    await agentSeeder.SeedAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<ITicketSeeder>();
    await seeder.SeedAsync();
}

app.UseCors("WebFrontend");

app.UseObservability();

// ─────────── Endpoints ────────────

var api = app.MapGroup("/api/v1");

api.MapGroup("/tickets")
    .WithTags("Tickets")
    .MapTicketsEndpoints();

api.MapGroup("/agents")
    .WithTags("Agents")
    .MapAgentsEndpoints();

api.MapGroup("/dashboard")
    .WithTags("Dashboard")
    .MapDashboardEndpoints();

api.MapGroup("/assistant")
    .WithTags("Assistant")
    .MapAssistantEndpoints();

app.Run();


public partial class Program
{
}
