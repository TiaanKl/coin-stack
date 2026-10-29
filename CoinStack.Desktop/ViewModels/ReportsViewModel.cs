using System;
using System.Threading.Tasks;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class ReportsViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ITransactionService _transactions;
    private readonly ISavingsService _savings;
    private readonly ISettingsService _settings;
    private readonly IMoneyFormatter _money;

    [ObservableProperty] private string _periodLabel = "Current period";
    [ObservableProperty] private string _periodIncomeText = "";
    [ObservableProperty] private string _periodExpenseText = "";
    [ObservableProperty] private string _periodNetText = "";
    [ObservableProperty] private string _lifetimeIncomeText = "";
    [ObservableProperty] private string _lifetimeExpenseText = "";
    [ObservableProperty] private string _lifetimeNetText = "";
    [ObservableProperty] private string _savingsAvailableText = "";
    [ObservableProperty] private bool _periodNetPositive = true;
    [ObservableProperty] private bool _lifetimeNetPositive = true;

    public ReportsViewModel(
        ITransactionService transactions,
        ISavingsService savings,
        ISettingsService settings,
        IMoneyFormatter money)
    {
        _transactions = transactions;
        _savings = savings;
        _settings = settings;
        _money = money;
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var settings = await _settings.GetAsync();
            var currency = settings.Currency;
            var now = DateTime.UtcNow;

            // Current calendar month window for the period summary
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);
            PeriodLabel = monthStart.ToString("MMMM yyyy");

            var period = await _transactions.GetIncomeExpenseForPeriodAsync(monthStart, monthEnd);
            var periodNet = period.Income - period.Expense;
            PeriodIncomeText = _money.Format(currency, period.Income);
            PeriodExpenseText = _money.Format(currency, period.Expense);
            PeriodNetText = _money.Format(currency, periodNet);
            PeriodNetPositive = periodNet >= 0;

            var lifetime = await _transactions.GetLifetimeTotalsAsync();
            var lifetimeNet = lifetime.TotalIncome - lifetime.TotalExpense;
            LifetimeIncomeText = _money.Format(currency, lifetime.TotalIncome);
            LifetimeExpenseText = _money.Format(currency, lifetime.TotalExpense);
            LifetimeNetText = _money.Format(currency, lifetimeNet);
            LifetimeNetPositive = lifetimeNet >= 0;

            var savings = await _savings.GetStateAsync();
            SavingsAvailableText = _money.Format(currency, savings.Available);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load reports: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
