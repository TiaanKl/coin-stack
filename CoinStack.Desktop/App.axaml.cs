using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CoinStack.Data;
using CoinStack.Desktop.ViewModels;
using CoinStack.Desktop.Views;
using CoinStack.Services;
using Microsoft.Extensions.DependencyInjection;
using SQLitePCL;

namespace CoinStack.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Batteries_V2.Init();

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        DatabaseInitializer.InitializeAsync(Services).GetAwaiter().GetResult();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CoinStack");
        Directory.CreateDirectory(appData);
        var dbPath = Path.Combine(appData, "coinstack.desktop.db");

        services.AddFinanceManagerData($"Data Source={dbPath}");
        services.AddFinanceManagerAppServices();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<TransactionsViewModel>();
        services.AddTransient<BudgetsViewModel>();
        services.AddTransient<GoalsViewModel>();
        services.AddTransient<DebtViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<IncomeViewModel>();
        services.AddTransient<SavingsViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<SubscriptionsViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
