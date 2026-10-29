using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using CoinStack.Services;

namespace CoinStack.Mobile;

public partial class App : Application
{
	private readonly IMobileDatabaseInitializationService _databaseInitializationService;
	private readonly IDailyCatchUpService _catchUp;
	private Task _initialization = Task.CompletedTask;

	public App(IMobileDatabaseInitializationService databaseInitializationService, IDailyCatchUpService catchUp)
	{
		_databaseInitializationService = databaseInitializationService;
		_catchUp = catchUp;
		InitializeComponent();

		// Apply saved theme (Light / Dark / System) before any UI is created
		AppThemeManager.ApplyTheme();

		_initialization = InitializeDatabaseAsync();
		_ = RunCatchUpAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell()) { Title = "CoinStack" };
	}

	// A phone app can sit in the background for days, so coming back to it may be a new day.
	protected override void OnResume()
	{
		base.OnResume();
		_ = RunCatchUpAsync();
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

	private async Task RunCatchUpAsync()
	{
		try
		{
			await _initialization;
			await _catchUp.RunIfDueAsync();
		}
		catch (Exception exception)
		{
			System.Diagnostics.Debug.WriteLine($"Daily catch-up failed: {exception.Message}");
		}
	}
}
