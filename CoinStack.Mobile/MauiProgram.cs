using CoinStack.Mobile.Core;
using CoinStack.Mobile.Services;
using CoinStack.Data;
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
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddCoinStackMobileCore();

		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "coinstack.mobile.db");
		builder.Services.AddFinanceManagerData($"Data Source={dbPath}");
		builder.Services.AddSingleton<IMobileDatabaseInitializationService, MobileDatabaseInitializationService>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
