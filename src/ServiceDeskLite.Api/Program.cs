using System.Text.Json.Serialization;
using Serilog;

using ServiceDeskLite.Api.Composition;
using ServiceDeskLite.Api.Endpoints;
using ServiceDeskLite.Application.DependencyInjection;

// ──────────── Logging ────────────

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .CreateLogger();

// ──────────── Builder ────────────

var builder = WebApplication.CreateBuilder(args);

// ──────────── Services ────────────

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddApiDocumentation()                          // OpenAPI
    .AddApiErrorHandling()                          // ErrorHandling + ProblemDetails + Mapper
    .AddApplication()                               // Application Layer
    .AddApiInfrastructure(builder.Configuration);   // Infrastructure Provider Switch

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebDev",
        p => p
            .WithOrigins("https://localhost:7023")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// ─────────── Middleware ───────────

app.UseSerilogRequestLogging();

app.UseApiDocumentation();

app.UseApiErrorHandling();
app.UseHttpsRedirection();

app.UseCors("WebDev");

// ─────────── Endpoints ────────────

var api = app.MapGroup("/api/v1");

api.MapGroup("/tickets")
    .WithTags("Tickets")
    .MapTicketsEndpoints();

app.Run();


public partial class Program { }
