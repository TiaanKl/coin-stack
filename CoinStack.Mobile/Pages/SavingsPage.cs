using CoinStack.Mobile.Core;
using CoinStack.Mobile.Services;

namespace CoinStack.Mobile.Pages;

public sealed class SavingsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Label _totalLabel;
    private readonly Label _availableLabel;
    private readonly Label _reservedLabel;
    private readonly Label _fallbackLabel;
    private readonly Label _lastCalcLabel;
    private readonly Switch _fallbackToggle;
    private readonly VerticalStackLayout _projectionList;
    private readonly VerticalStackLayout _summaryList;
    private bool _isHydrating;

    public SavingsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Savings";

        _totalLabel = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold };
        _availableLabel = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold };
        _reservedLabel = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold };
        _fallbackLabel = new Label { FontSize = 14 };
        _lastCalcLabel = new Label { FontSize = 12 };
        _fallbackToggle = new Switch();
        _fallbackToggle.Toggled += async (_, e) =>
        {
            if (_isHydrating)
            {
                return;
            }

            await _financeService.SetSavingsFallbackEnabledAsync(e.Value);
        };

        var runMonthButton = new Button { Text = "Apply Monthly Savings" };
        runMonthButton.Clicked += async (_, _) => await ApplyMonthlyAsync();

        var withdrawButton = new Button { Text = "Withdraw (Emergency)" };
        withdrawButton.Clicked += async (_, _) => await WithdrawAsync();

        _projectionList = new VerticalStackLayout { Spacing = 6 };
        _summaryList = new VerticalStackLayout { Spacing = 6 };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    _totalLabel,
                    _availableLabel,
                    _reservedLabel,
                    _fallbackLabel,
                    _lastCalcLabel,
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Label { Text = "Fallback Enabled", VerticalTextAlignment = TextAlignment.Center },
                            _fallbackToggle
                        }
                    },
                    runMonthButton,
                    withdrawButton,
                    new BoxView { HeightRequest = 1 },
                    new Label { Text = "Savings Projection", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _projectionList,
                    new BoxView { HeightRequest = 1 },
                    new Label { Text = "Recent Monthly Summaries", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _summaryList
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task ApplyMonthlyAsync()
    {
        var applied = await _financeService.CalculateSavingsForCurrentMonthAsync();
        if (!applied)
        {
            await DisplayAlertAsync("Info", "Savings for this month were already calculated.", "OK");
        }

        await LoadAsync();
    }

    private async Task WithdrawAsync()
    {
        var amountInput = await DisplayPromptAsync("Withdraw Savings", "Amount:", "Withdraw", "Cancel", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(amountInput) || !decimal.TryParse(amountInput, out var amount))
        {
            return;
        }

        var reason = await DisplayPromptAsync("Withdraw Savings", "Reason:", "Save", "Cancel");
        var withdrawn = await _financeService.WithdrawSavingsAsync(amount, reason ?? "Manual");
        await DisplayAlertAsync("Savings", $"Withdrawn: {withdrawn:0.00}", "OK");

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _isHydrating = true;
            var settings = await _financeService.GetSettingsAsync();
            var snapshot = await _financeService.GetSavingsSnapshotAsync();

            _totalLabel.Text = $"Total: {MoneyDisplay.Format(settings.Currency, snapshot.State.Total)}";
            _availableLabel.Text = $"Available: {MoneyDisplay.Format(settings.Currency, snapshot.State.Available)}";
            _reservedLabel.Text = $"Reserved: {MoneyDisplay.Format(settings.Currency, snapshot.State.Reserved)}";
            _fallbackLabel.Text = $"Fallback used this month: {MoneyDisplay.Format(settings.Currency, snapshot.FallbackUsedThisMonth)}";
            _lastCalcLabel.Text = $"Last calculated month: {snapshot.State.LastCalculatedMonth ?? "Never"}";
            _fallbackToggle.IsToggled = snapshot.State.FallbackEnabled;

            _projectionList.Children.Clear();
            foreach (var point in snapshot.Projections)
            {
                _projectionList.Children.Add(new Label
                {
                    Text = $"{point.Month}: {MoneyDisplay.Format(settings.Currency, point.Projected)}",
                    FontSize = 13
                });
            }

            _summaryList.Children.Clear();
            if (snapshot.MonthlySummaries.Count == 0)
            {
                _summaryList.Children.Add(new Label { Text = "No summaries yet." });
                return;
            }

            foreach (var summary in snapshot.MonthlySummaries.Take(12))
            {
                _summaryList.Children.Add(new Label
                {
                    Text = $"{summary.Month} • Base {MoneyDisplay.Format(settings.Currency, summary.Base)} • Interest {MoneyDisplay.Format(settings.Currency, summary.Interest)} • Running {MoneyDisplay.Format(settings.Currency, summary.RunningTotal)}",
                    FontSize = 12
                });
            }

            _isHydrating = false;
        }
        catch (Exception ex)
        {
            _isHydrating = false;
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
