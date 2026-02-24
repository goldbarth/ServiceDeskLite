using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

using Serilog;

using ServiceDeskLite.Api.Composition;
using ServiceDeskLite.Api.Endpoints;
using ServiceDeskLite.Application.DependencyInjection;
using ServiceDeskLite.Application.Tickets.Seeding;
using ServiceDeskLite.Infrastructure.Persistence;

// ──────────── Logging ────────────

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .CreateLogger();

// ──────────── Builder ────────────

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console();
});

// ──────────── Services ────────────

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddApiDocumentation() // OpenAPI
    .AddApiErrorHandling() // ErrorHandling + ProblemDetails + Mapper
    .AddApplication() // Application Layer
    .AddApiInfrastructure(builder.Configuration); // Infrastructure Provider Switch

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebDev",
        p => p
            .WithOrigins("https://localhost:7023")
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

if (app.Configuration["Persistence:Provider"] == "Sqlite")
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider
        .GetRequiredService<ServiceDeskLiteDbContext>()
        .Database.MigrateAsync();
}

// ─────────── Middleware ───────────

app.UseSerilogRequestLogging();

app.UseApiDocumentation();

app.UseApiErrorHandling();
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
    var seeder = scope.ServiceProvider.GetRequiredService<ITicketSeeder>();
    await seeder.SeedAsync();
}

app.UseCors("WebDev");

// ─────────── Endpoints ────────────

var api = app.MapGroup("/api/v1");

api.MapGroup("/tickets")
    .WithTags("Tickets")
    .MapTicketsEndpoints();

app.Run();


public partial class Program
{
}
