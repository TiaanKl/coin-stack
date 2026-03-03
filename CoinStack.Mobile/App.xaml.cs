using CoinStack.Mobile.Services;

namespace CoinStack.Mobile;

public partial class App : Application
{
	private readonly IMobileDatabaseInitializationService _databaseInitializationService;
	private readonly IMobileFinanceService _financeService;

	public App(
		IMobileDatabaseInitializationService databaseInitializationService,
		IMobileFinanceService financeService)
	{
		_databaseInitializationService = databaseInitializationService;
		_financeService = financeService;
		InitializeComponent();
		_ = InitializeDatabaseAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell(_financeService)) { Title = "CoinStack" };
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
