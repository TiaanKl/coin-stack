using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;

namespace CoinStack.Mobile;

public partial class App : Application
{
	private readonly IMobileDatabaseInitializationService _databaseInitializationService;

	public App(IMobileDatabaseInitializationService databaseInitializationService)
	{
		_databaseInitializationService = databaseInitializationService;
		InitializeComponent();

		// Apply saved theme (Light / Dark / System) before any UI is created
		AppThemeManager.ApplyTheme();

		_ = InitializeDatabaseAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell()) { Title = "CoinStack" };
	}

	private async Task InitializeDatabaseAsync()
	{
		try
		{
			await _databaseInitializationService.InitializeAsync();
		}
		catch (Exception exception)
		{
			System.Diagnostics.Debug.WriteLine($"Mobile DB init failed: {exception.Message}");
		}
	}
}
