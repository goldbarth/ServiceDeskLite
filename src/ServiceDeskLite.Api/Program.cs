using System.Text.Json.Serialization;
using ServiceDeskLite.Api.Composition;
using ServiceDeskLite.Api.Endpoints;
using ServiceDeskLite.Application.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ──────────── Services ────────────

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddApiDocumentation()                          // OpenAPI
    .AddApplication()                               // Application Layer
    .AddApiInfrastructure(builder.Configuration);   // Infrastructure Provider Switch

var app = builder.Build();

// ─────────── Middleware ───────────

app.UseApiDocumentation();

app.UseHttpsRedirection();

// ─────────── Endpoints ────────────

var api = app.MapGroup("/api/v1");

api.MapGroup("/tickets")
    .WithTags("Tickets")
    .MapTicketsEndpoints();

app.Run();
