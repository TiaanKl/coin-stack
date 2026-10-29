using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class IncomeViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ITransactionService _transactions;
    private readonly ISettingsService _settings;
    private readonly IMoneyFormatter _money;

    [ObservableProperty] private string _monthlyIncomeText = "";
    [ObservableProperty] private string _periodIncomeText = "";
    [ObservableProperty] private string _lifetimeIncomeText = "";
    [ObservableProperty] private string _periodLabel = "This period";

    public ObservableCollection<TransactionRow> IncomeTransactions { get; } = [];

    public IncomeViewModel(
        ITransactionService transactions,
        ISettingsService settings,
        IMoneyFormatter money)
    {
        _transactions = transactions;
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
            var period = BudgetPeriod.Containing(settings.MonthStartDay, now);

            MonthlyIncomeText = _money.Format(currency, settings.MonthlyIncome);
            PeriodLabel = $"{period.FirstDay:MMM dd} – {period.LastDay:MMM dd}";

            var totals = await _transactions.GetIncomeExpenseForPeriodAsync(period.StartUtc, period.EndUtc);
            PeriodIncomeText = _money.Format(currency, totals.Income);

            var lifetime = await _transactions.GetLifetimeTotalsAsync();
            LifetimeIncomeText = _money.Format(currency, lifetime.TotalIncome);

            IncomeTransactions.Clear();
            var income = (await _transactions.GetAllAsync())
                .Where(t => t.Type == TransactionType.Income)
                .OrderByDescending(t => t.OccurredAtUtc);

            foreach (var tx in income)
            {
                IncomeTransactions.Add(new TransactionRow
                {
                    Id = tx.Id,
                    Date = tx.OccurredAtUtc,
                    Description = tx.Description,
                    Category = tx.Category?.Name ?? "Uncategorized",
                    TypeLabel = "Income",
                    AmountText = _money.Format(currency, tx.Amount),
                    IsIncome = true
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load income: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
