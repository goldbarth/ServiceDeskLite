using MudBlazor.Services;
using ServiceDeskLite.Web.Components;
using ServiceDeskLite.Web.Composition;
using ServiceDeskLite.Web.Features.Tickets;

var builder = WebApplication.CreateBuilder(args);

// ──────────── Services ────────────

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = true;
});

builder.Services
    .AddTicketsApiClient(builder.Configuration)
    .AddAssistantApiClient(builder.Configuration)
    .AddTicketSummaryApiClient(builder.Configuration)
    .AddTicketsFeature();

var app = builder.Build();

// ─────────── Middleware ───────────

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
