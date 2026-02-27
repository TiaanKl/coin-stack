using CoinStack.Components;
using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services
    .AddIdentityApiEndpoints<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    })
    .AddEntityFrameworkStores<CoinStackDbContext>();

builder.Services.AddAuthorization();

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

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGroup("/auth").MapIdentityApi<ApplicationUser>();

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