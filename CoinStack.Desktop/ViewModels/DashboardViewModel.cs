using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ITransactionService _transactions;
    private readonly IBucketService _buckets;
    private readonly ISavingsService _savings;
    private readonly IScoringService _scoring;
    private readonly ISettingsService _settings;
    private readonly IMoneyFormatter _money;

    [ObservableProperty] private string _budgetRemainingText = "";
    [ObservableProperty] private string _budgetSpentText = "";
    [ObservableProperty] private string _budgetLimitText = "";
    [ObservableProperty] private string _reservesTotalText = "";
    [ObservableProperty] private string _emergencyFundText = "";
    [ObservableProperty] private string _savingsAvailableText = "";
    [ObservableProperty] private double _budgetProgress;
    [ObservableProperty] private double _budgetProgressMax = 100;
    [ObservableProperty] private bool _isOverBudget;
    [ObservableProperty] private int _totalScore;

    public ObservableCollection<TransactionRow> RecentTransactions { get; } = [];
    public ObservableCollection<ScoreEventRow> RecentScoreEvents { get; } = [];

    public DashboardViewModel(
        ITransactionService transactions,
        IBucketService buckets,
        ISavingsService savings,
        IScoringService scoring,
        ISettingsService settings,
        IMoneyFormatter money)
    {
        _transactions = transactions;
        _buckets = buckets;
        _savings = savings;
        _scoring = scoring;
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

            var buckets = await _buckets.GetAllAsync();
            var spentMap = await _buckets.GetSpentAmountsForPeriodAsync(settings.MonthStartDay, now);

            var allocated = buckets.Sum(b => b.AllocatedAmount);
            var spent = buckets.Sum(b => spentMap.TryGetValue(b.Id, out var s) ? s : 0m);
            var remaining = allocated - spent;

            BudgetLimitText = _money.Format(currency, allocated);
            BudgetSpentText = _money.Format(currency, spent);
            BudgetRemainingText = _money.Format(currency, remaining);
            BudgetProgress = (double)spent;
            BudgetProgressMax = allocated > 0 ? (double)allocated : 1;
            IsOverBudget = spent > allocated && allocated > 0;

            var reserves = await _savings.GetStateAsync();
            ReservesTotalText = _money.Format(currency, reserves.Total);
            EmergencyFundText = _money.Format(currency, reserves.EmergencyTotal);
            SavingsAvailableText = _money.Format(currency, reserves.Available);

            TotalScore = await _scoring.GetTotalScoreAsync();

            RecentTransactions.Clear();
            foreach (var tx in await _transactions.GetRecentAsync(8))
            {
                RecentTransactions.Add(ToRow(tx, currency));
            }

            RecentScoreEvents.Clear();
            foreach (var ev in await _scoring.GetRecentEventsAsync(8))
            {
                RecentScoreEvents.Add(new ScoreEventRow
                {
                    Description = string.IsNullOrWhiteSpace(ev.Description) ? ev.Reason.ToString() : ev.Description,
                    PointsText = ev.Points > 0 ? $"+{ev.Points}" : ev.Points.ToString(),
                    IsPositive = ev.Points >= 0,
                    DateText = ev.CreatedAtUtc.ToLocalTime().ToString("MMM dd")
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load dashboard: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private TransactionRow ToRow(Transaction tx, string currency) => new()
    {
        Id = tx.Id,
        Date = tx.OccurredAtUtc,
        Description = tx.Description,
        Category = tx.Category?.Name ?? "Uncategorized",
        TypeLabel = tx.Type.ToString(),
        AmountText = _money.Format(currency, tx.Amount),
        IsIncome = tx.Type == TransactionType.Income
    };
}
