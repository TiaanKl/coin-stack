using CoinStack.Components;
using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using ApexCharts;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;

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
builder.Services.AddHostedService<DailyCatchUpHostedService>();

StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

// Format money in the saved currency from the very first render, not only after a page has loaded
// settings: otherwise a page opened first (such as Debt) showed "$" amounts.
await using (var scope = app.Services.CreateAsyncScope())
{
    var settings = await scope.ServiceProvider.GetRequiredService<ISettingsService>().GetAsync();
    CurrencyFormatting.ApplyCurrency(settings.Currency);
}

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