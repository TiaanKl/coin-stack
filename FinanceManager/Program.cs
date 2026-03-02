using CoinStack.Components;
using CoinStack.Data;
using CoinStack.Services;
using ApexCharts;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddApexCharts(options =>
{
    options.GlobalOptions = new ApexChartBaseOptions
    {
        Debug = true,
        Chart = new Chart
        {
            Animations = new Animations { Enabled = false },
            Toolbar = new Toolbar { Show = false },
            Zoom = new Zoom { Enabled = false }
        },
        DataLabels = new DataLabels { Enabled = false }
    };
});

builder.Services.AddFinanceManagerData(builder.Configuration);
builder.Services.AddFinanceManagerAppServices();

StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var settingsService = context.RequestServices.GetRequiredService<ISettingsService>();
    var settings = await settingsService.GetAsync(context.RequestAborted);
    CurrencyFormatting.ApplyCurrency(settings.Currency);
    await next();
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (app.Environment.IsDevelopment())
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var url = app.Urls.FirstOrDefault(u => u.StartsWith("https"))
               ?? app.Urls.FirstOrDefault()
               ?? "https://localhost:5001";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    });
}

app.Run();