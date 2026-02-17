using ServiceDeskLite.Web.Components;
using ServiceDeskLite.Web.Composition;

var builder = WebApplication.CreateBuilder(args);

// ──────────── Services ────────────

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddTicketsApiClient(builder.Configuration)
    .AddWebInfrastructure(builder.Configuration);

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
