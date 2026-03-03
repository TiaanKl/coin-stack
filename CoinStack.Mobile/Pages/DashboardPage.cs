using CoinStack.Mobile.Core;
using CoinStack.Mobile.Services;

namespace CoinStack.Mobile.Pages;

public sealed class DashboardPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Label _incomeLabel;
    private readonly Label _expenseLabel;
    private readonly Label _netLabel;
    private readonly Label _bucketCountLabel;
    private readonly Label _scoreLabel;
    private readonly Label _streakLabel;
    private readonly Label _subsLabel;
    private readonly Label _debtsLabel;
    private readonly Label _reflectionsLabel;
    private readonly Label _recentHeaderLabel;
    private readonly VerticalStackLayout _recentList;

    public DashboardPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Dashboard";

        _incomeLabel = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold };
        _expenseLabel = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold };
        _netLabel = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold };
        _bucketCountLabel = new Label { FontSize = 14 };
        _scoreLabel = new Label { FontSize = 14 };
        _streakLabel = new Label { FontSize = 14 };
        _subsLabel = new Label { FontSize = 14 };
        _debtsLabel = new Label { FontSize = 14 };
        _reflectionsLabel = new Label { FontSize = 14 };
        _recentHeaderLabel = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, Text = "Recent Transactions" };
        _recentList = new VerticalStackLayout { Spacing = 8 };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Overview", FontSize = 22, FontAttributes = FontAttributes.Bold },
                    _incomeLabel,
                    _expenseLabel,
                    _netLabel,
                    _bucketCountLabel,
                    _scoreLabel,
                    _streakLabel,
                    _subsLabel,
                    _debtsLabel,
                    _reflectionsLabel,
                    new BoxView { HeightRequest = 1 },
                    _recentHeaderLabel,
                    _recentList
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            await _financeService.ProcessDailyCheckInAsync();
            var settings = await _financeService.GetSettingsAsync();
            var snapshot = await _financeService.GetDashboardSnapshotAsync();

            var currency = settings.Currency;
            _incomeLabel.Text = $"Income: {MoneyDisplay.Format(currency, snapshot.TotalIncome)}";
            _expenseLabel.Text = $"Spent: {MoneyDisplay.Format(currency, snapshot.TotalExpense)}";
            _netLabel.Text = $"Net Saved: {MoneyDisplay.Format(currency, snapshot.NetSaved)}";
            _bucketCountLabel.Text = $"Buckets: {snapshot.Buckets.Count}";
            _scoreLabel.Text = $"Total Score: {snapshot.TotalScore} pts";
            _streakLabel.Text = $"Daily Check-in Streak: {snapshot.DailyCheckInStreak} day(s)";
            _subsLabel.Text = $"Active Subscriptions: {snapshot.ActiveSubscriptions}";
            _debtsLabel.Text = $"Debt Accounts: {snapshot.ActiveDebts}";
            _reflectionsLabel.Text = $"Pending Reflections: {snapshot.PendingReflections}";

            _recentList.Children.Clear();
            if (snapshot.RecentTransactions.Count == 0)
            {
                _recentList.Children.Add(new Label { Text = "No transactions yet." });
                return;
            }

            foreach (var tx in snapshot.RecentTransactions)
            {
                var amount = MoneyDisplay.Format(currency, tx.Amount);
                var prefix = tx.Type == CoinStack.Data.Entities.TransactionType.Income ? "+" : "-";

                _recentList.Children.Add(new Label
                {
                    Text = $"{tx.OccurredAtUtc:dd MMM} • {tx.Description} • {prefix}{amount}",
                    FontSize = 14
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
