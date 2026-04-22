using CoinStack.Mobile.Core;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

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

        _totalLabel = new Label { FontSize = 14, FontFamily = "InterBold", TextColor = AppColors.Dark };
        _availableLabel = new Label { FontSize = 14, FontFamily = "InterBold", TextColor = AppColors.Success };
        _reservedLabel = new Label { FontSize = 14, FontFamily = "InterBold", TextColor = AppColors.Warning };
        _fallbackLabel = new Label { FontSize = 13, FontFamily = "InterRegular", TextColor = AppColors.Muted };
        _lastCalcLabel = new Label { FontSize = 12, FontFamily = "InterRegular", TextColor = AppColors.Muted };
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

        // ── Overview Card ──
        var overviewCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Savings Overview", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                CreateStatRow("Total", _totalLabel),
                CreateStatRow("Available", _availableLabel),
                CreateStatRow("Reserved", _reservedLabel),
                new BoxView { HeightRequest = 1, Color = AppColors.Border },
                _fallbackLabel,
                _lastCalcLabel
            }
        });

        // ── Settings Card ──
        var settingsCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    Children =
                    {
                        new Label { Text = "Fallback Enabled", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark, VerticalOptions = LayoutOptions.Center },
                    }
                },
                runMonthButton,
                withdrawButton
            }
        });
        ((Grid)((VerticalStackLayout)settingsCard.Content).Children[0]).Add(_fallbackToggle, 1, 0);

        // ── Projection Card ──
        var projectionCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Savings Projection", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _projectionList
            }
        });

        // ── Summaries Card ──
        var summariesCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Recent Monthly Summaries", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _summaryList
            }
        });

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children = { overviewCard, settingsCard, projectionCard, summariesCard }
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
                var row = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    Padding = new Thickness(12, 8)
                };
                row.Add(new Label { Text = point.Month, FontFamily = "InterRegular", FontSize = 13, TextColor = AppColors.Dark, VerticalOptions = LayoutOptions.Center }, 0, 0);
                row.Add(new Label { Text = MoneyDisplay.Format(settings.Currency, point.Projected), FontFamily = "InterBold", FontSize = 13, TextColor = AppColors.Success, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center }, 1, 0);

                _projectionList.Children.Add(new Border
                {
                    BackgroundColor = AppColors.SurfaceDim,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Stroke = Brush.Transparent,
                    Content = row
                });
            }

            _summaryList.Children.Clear();
            if (snapshot.MonthlySummaries.Count == 0)
            {
                _summaryList.Children.Add(new Label { Text = "No summaries yet.", FontFamily = "InterRegular", TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 16) });
                return;
            }

            foreach (var summary in snapshot.MonthlySummaries.Take(12))
            {
                var summaryRow = new VerticalStackLayout { Spacing = 4 };
                var header = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                };
                header.Add(new Label { Text = summary.Month, FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark }, 0, 0);
                header.Add(new Label { Text = MoneyDisplay.Format(settings.Currency, summary.RunningTotal), FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Success, HorizontalOptions = LayoutOptions.End }, 1, 0);
                summaryRow.Children.Add(header);
                summaryRow.Children.Add(new Label
                {
                    Text = $"Base {MoneyDisplay.Format(settings.Currency, summary.Base)} · Interest {MoneyDisplay.Format(settings.Currency, summary.Interest)}",
                    FontSize = 11,
                    FontFamily = "InterRegular",
                    TextColor = AppColors.Muted
                });

                _summaryList.Children.Add(new Border
                {
                    BackgroundColor = AppColors.SurfaceDim,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Stroke = Brush.Transparent,
                    Padding = new Thickness(12, 10),
                    Content = summaryRow
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

    private static Border CreateCard(View content) => new()
    {
        BackgroundColor = AppColors.Surface,
        StrokeShape = new RoundRectangle { CornerRadius = 16 },
        Stroke = new SolidColorBrush(AppColors.Border),
        StrokeThickness = 1,
        Padding = new Thickness(16),
        Content = content
    };

    private static View CreateStatRow(string label, Label valueLabel)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Padding = new Thickness(12, 8)
        };
        grid.Add(new Label { Text = label, FontFamily = "InterRegular", FontSize = 13, TextColor = AppColors.Muted, VerticalOptions = LayoutOptions.Center }, 0, 0);
        valueLabel.HorizontalOptions = LayoutOptions.End;
        valueLabel.VerticalOptions = LayoutOptions.Center;
        grid.Add(valueLabel, 1, 0);

        return new Border
        {
            BackgroundColor = AppColors.SurfaceDim,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Stroke = Brush.Transparent,
            Content = grid
        };
    }
}
