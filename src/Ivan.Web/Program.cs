using System.Globalization;
using Ivan.Application.Services;
using Ivan.Infrastructure;
using Ivan.Infrastructure.Data;
using Ivan.Web.Components;
using Ivan.Web.Services;
using Microsoft.AspNetCore.Localization;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Radzen components
builder.Services.AddRadzenComponents();

// Application Services
builder.Services.AddSingleton<ICalendarService, CalendarService>();
builder.Services.AddScoped<UserPreferencesState>();

// Infrastructure (EF Core SQLite, Identity, Services)
builder.Services.AddInfrastructure(builder.Configuration);

// Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
var supportedCultures = new[]
{
    new CultureInfo("fa-IR"),
    new CultureInfo("en-US")
};

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("fa-IR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

var app = builder.Build();

// Ensure SQLite database and directory exist
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IvanDbContext>();
    Directory.CreateDirectory("app_data");
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseRequestLocalization();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
