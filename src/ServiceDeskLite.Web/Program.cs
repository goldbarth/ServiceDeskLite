using ServiceDeskLite.Infrastructure.InMemory.DependencyInjection;
using ServiceDeskLite.Infrastructure.Persistence.DependencyInjection;
using ServiceDeskLite.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var provider = builder.Configuration["Persistence:Provider"] ?? "Sqlite";

switch (provider)
{
    case "InMemory":
        builder.Services.AddInfrastructureInMemory();
        break;

    case "Sqlite":
        builder.Services.AddInfrastructure(builder.Configuration);
        break;

    default:
        throw new InvalidOperationException($"Unknown persistence provider: '{provider}'.");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
