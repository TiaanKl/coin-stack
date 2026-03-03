using CoinStack.Mobile.Core;
using CoinStack.Mobile.Services;

namespace CoinStack.Mobile.Pages;

public sealed class ReportsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Label _incomeLabel;
    private readonly Label _expenseLabel;
    private readonly Label _netLabel;
    private readonly Label _savingsLabel;
    private readonly Label _fallbackLabel;
    private readonly Label _debtCountLabel;
    private readonly Label _debtOutstandingLabel;
    private readonly Label _debtMonthlyLabel;
    private readonly VerticalStackLayout _bucketList;
    private readonly VerticalStackLayout _dailyList;

    public ReportsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Reports";

        _incomeLabel = new Label { FontAttributes = FontAttributes.Bold, FontSize = 14 };
        _expenseLabel = new Label { FontAttributes = FontAttributes.Bold, FontSize = 14 };
        _netLabel = new Label { FontAttributes = FontAttributes.Bold, FontSize = 14 };
        _savingsLabel = new Label { FontSize = 14 };
        _fallbackLabel = new Label { FontSize = 12 };
        _debtCountLabel = new Label { FontSize = 14 };
        _debtOutstandingLabel = new Label { FontSize = 14 };
        _debtMonthlyLabel = new Label { FontSize = 14 };
        _bucketList = new VerticalStackLayout { Spacing = 6 };
        _dailyList = new VerticalStackLayout { Spacing = 6 };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    new Label { Text = "Reports Snapshot", FontSize = 22, FontAttributes = FontAttributes.Bold },
                    _incomeLabel,
                    _expenseLabel,
                    _netLabel,
                    _savingsLabel,
                    _fallbackLabel,
                    _debtCountLabel,
                    _debtOutstandingLabel,
                    _debtMonthlyLabel,
                    new BoxView { HeightRequest = 1 },
                    new Label { Text = "Top Bucket Spending", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _bucketList,
                    new BoxView { HeightRequest = 1 },
                    new Label { Text = "Last 14 Days Net", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _dailyList
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
            var settings = await _financeService.GetSettingsAsync();
            var report = await _financeService.GetReportSnapshotAsync();

            _incomeLabel.Text = $"Income (Period): {MoneyDisplay.Format(settings.Currency, report.PeriodIncome)}";
            _expenseLabel.Text = $"Expenses (Period): {MoneyDisplay.Format(settings.Currency, report.PeriodExpense)}";
            _netLabel.Text = $"Net Cashflow: {MoneyDisplay.Format(settings.Currency, report.PeriodNet)}";
            _savingsLabel.Text = $"Savings Available: {MoneyDisplay.Format(settings.Currency, report.SavingsAvailable)}";
            _fallbackLabel.Text = $"Fallback Used This Month: {MoneyDisplay.Format(settings.Currency, report.FallbackUsedThisMonth)}";
            _debtCountLabel.Text = $"Open Debts: {report.OpenDebtCount}";
            _debtOutstandingLabel.Text = $"Debt Outstanding: {MoneyDisplay.Format(settings.Currency, report.TotalDebtOutstanding)}";
            _debtMonthlyLabel.Text = $"Debt Monthly Payments: {MoneyDisplay.Format(settings.Currency, report.TotalDebtMonthlyPayment)}";

            _bucketList.Children.Clear();
            if (report.TopBucketSpending.Count == 0)
            {
                _bucketList.Children.Add(new Label { Text = "No expense data for this period." });
            }
            else
            {
                foreach (var row in report.TopBucketSpending)
                {
                    _bucketList.Children.Add(new Label
                    {
                        Text = $"{row.Name} • {MoneyDisplay.Format(settings.Currency, row.Spent)} / {MoneyDisplay.Format(settings.Currency, row.Allocated)}",
                        FontSize = 13
                    });
                }
            }

            _dailyList.Children.Clear();
            foreach (var row in report.DailyNet14Days)
            {
                _dailyList.Children.Add(new Label
                {
                    Text = $"{row.Date:dd MMM} • In {MoneyDisplay.Format(settings.Currency, row.Income)} • Out {MoneyDisplay.Format(settings.Currency, row.Expense)} • Net {MoneyDisplay.Format(settings.Currency, row.Net)}",
                    FontSize = 12
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
