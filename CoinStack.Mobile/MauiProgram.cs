using CoinStack.Data;
using CoinStack.Mobile.Core;
using CoinStack.Mobile.Pages;
using CoinStack.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace CoinStack.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");

				// Typography - Inter
				fonts.AddFont("Inter-Regular.ttf", "InterRegular");
				fonts.AddFont("Inter-Medium.ttf", "InterMedium");
				fonts.AddFont("Inter-Bold.ttf", "InterBold");
				fonts.AddFont("Inter-Light.ttf", "InterLight");

				// Icons - Font Awesome 7
				fonts.AddFont("Font Awesome 7 Free-Regular-400.otf", "FontAwesomeRegular");
				fonts.AddFont("Font Awesome 7 Free-Solid-900.otf", "FontAwesomeSolid");
				fonts.AddFont("Font Awesome 7 Brands-Regular-400.otf", "FontAwesomeBrands");
			});

		builder.Services.AddCoinStackMobileCore();

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "coinstack.mobile.db");
		builder.Services.AddFinanceManagerData($"Data Source={dbPath}");
		builder.Services.AddSingleton<IMobileDatabaseInitializationService, MobileDatabaseInitializationService>();
		builder.Services.AddSingleton<IMobileFinanceService, MobileFinanceService>();

		// Register all pages for Shell DI resolution
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<MoneyHubPage>();
		builder.Services.AddTransient<GoalsHubPage>();
		builder.Services.AddTransient<GrowthHubPage>();
		builder.Services.AddTransient<MoreHubPage>();
		builder.Services.AddTransient<TransactionsPage>();
		builder.Services.AddTransient<IncomePage>();
		builder.Services.AddTransient<BucketsPage>();
		builder.Services.AddTransient<GoalsPage>();
		builder.Services.AddTransient<SavingsPage>();
		builder.Services.AddTransient<SubscriptionsPage>();
		builder.Services.AddTransient<CategoriesPage>();
		builder.Services.AddTransient<DebtPage>();
		builder.Services.AddTransient<DebtSimulatorPage>();
		builder.Services.AddTransient<FallbackHistoryPage>();
		builder.Services.AddTransient<ReflectionsPage>();
		builder.Services.AddTransient<CbtJournalPage>();
		builder.Services.AddTransient<ChallengesPage>();
		builder.Services.AddTransient<AchievementsPage>();
		builder.Services.AddTransient<WaitlistPage>();
		builder.Services.AddTransient<WeeklyRecapMobilePage>();
		builder.Services.AddTransient<ReportsPage>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
