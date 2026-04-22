using CoinStack.Data.Entities;
using CoinStack.Mobile.Core;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class DashboardPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    // Header
    private Label _greetingLabel = null!;
    // Budget card
    private Label _budgetTitleLabel = null!;
    private Label _budgetSubtitleLabel = null!;
    private Label _plannedLabel = null!;
    private Label _spentLabel = null!;
    private Label _availableLabel = null!;
    private ProgressBar _budgetProgress = null!;
    // Reserves card
    private Label _savingsLabel = null!;
    private Label _emergencyLabel = null!;
    private Label _totalReservedLabel = null!;
    // Lists
    private VerticalStackLayout _recentTxList = null!;
    private VerticalStackLayout _scoreEventList = null!;

    public DashboardPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        BuildContent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        BuildContent();
        await LoadAsync();
    }

    private void BuildContent()
    {
        Title = "Dashboard";

        // ── Header ──
        _greetingLabel = new Label { Text = "Good morning", FontFamily = "InterBold", FontSize = 20, TextColor = AppColors.Dark };

        var bellIcon = new Border
        {
            WidthRequest = 44,
            HeightRequest = 44,
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            BackgroundColor = AppColors.Surface,
            Stroke = Brush.Transparent,
            Content = new Label { Text = AppIcons.GlyphBell, FontFamily = "FontAwesomeSolid", FontSize = 20, TextColor = AppColors.Dark, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        };

        var topHeader = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Margin = new Thickness(0, 10, 0, 0)
        };
        topHeader.Add(_greetingLabel, 0, 0);
        topHeader.Add(bellIcon, 1, 0);

        // ── Monthly Budget Remaining card ──
        _budgetTitleLabel = new Label { Text = "Monthly Budget Remaining", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark };
        _budgetSubtitleLabel = new Label { FontSize = 13, TextColor = AppColors.Muted, FontFamily = "InterRegular" };
        _budgetProgress = new ProgressBar { ProgressColor = AppColors.Accent, HeightRequest = 8 };

        _plannedLabel = new Label { FontSize = 12, FontFamily = "InterBold" };
        _spentLabel = new Label { FontSize = 12, FontFamily = "InterBold" };
        _availableLabel = new Label { FontSize = 12, FontFamily = "InterBold" };

        var budgetBadges = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            Children =
            {
                WrapBadge(_plannedLabel, AppColors.SurfaceContainer, AppColors.Muted),
                WrapBadge(_spentLabel, AppColors.BgDanger, AppColors.Danger),
                WrapBadge(_availableLabel, AppColors.BgSuccess, AppColors.Success)
            }
        };

        var budgetCard = new Border
        {
            BackgroundColor = AppColors.Surface,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = new SolidColorBrush(AppColors.Border),
            StrokeThickness = 1,
            Padding = new Thickness(16),
            Content = new VerticalStackLayout { Spacing = 10, Children = { _budgetTitleLabel, _budgetSubtitleLabel, _budgetProgress, budgetBadges } }
        };

        // ── Reserves On Hand card ──
        _savingsLabel = new Label { FontSize = 14, FontFamily = "InterBold", TextColor = AppColors.Success };
        _emergencyLabel = new Label { FontSize = 14, FontFamily = "InterBold", TextColor = AppColors.Warning };
        _totalReservedLabel = new Label { FontSize = 14, FontFamily = "InterBold", TextColor = AppColors.Dark };

        var reservesCard = new Border
        {
            BackgroundColor = AppColors.Surface,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = new SolidColorBrush(AppColors.Border),
            StrokeThickness = 1,
            Padding = new Thickness(16),
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label { Text = "Reserves On Hand", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                    CreateReserveRow("Savings", _savingsLabel),
                    CreateReserveRow("Emergency Fund", _emergencyLabel),
                    new BoxView { HeightRequest = 1, Color = AppColors.Border },
                    CreateReserveRow("Total Reserved", _totalReservedLabel)
                }
            }
        };

        // ── Recent Transactions ──
        _recentTxList = new VerticalStackLayout { Spacing = 8 };
        var txCard = new Border
        {
            BackgroundColor = AppColors.Surface,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = new SolidColorBrush(AppColors.Border),
            StrokeThickness = 1,
            Padding = new Thickness(16),
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = "Recent Transactions", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                    _recentTxList
                }
            }
        };

        // ── Recent Score Activity ──
        _scoreEventList = new VerticalStackLayout { Spacing = 8 };
        var scoreCard = new Border
        {
            BackgroundColor = AppColors.Surface,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = new SolidColorBrush(AppColors.Border),
            StrokeThickness = 1,
            Padding = new Thickness(16),
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = "Recent Score Activity", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                    _scoreEventList
                }
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children = { topHeader, budgetCard, reservesCard, txCard, scoreCard }
            }
        };
    }

    private async Task LoadAsync()
    {
        try
        {
            await _financeService.ProcessDailyCheckInAsync();
            var settings = await _financeService.GetSettingsAsync();
            var snapshot = await _financeService.GetDashboardSnapshotAsync();
            var savings = await _financeService.GetSavingsSnapshotAsync();
            var scoreEvents = await _financeService.GetRecentScoreEventsAsync(15);

            var currency = settings.Currency;
            var hour = DateTime.Now.Hour;
            var greeting = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
            _greetingLabel.Text = greeting;

            // Budget
            var budgetLimit = settings.MonthlyIncome;
            var budgetSpent = snapshot.TotalExpense;
            var planned = snapshot.Buckets.Sum(b => Math.Max(0, b.AllocatedAmount));
            var available = budgetLimit - budgetSpent;
            var progress = budgetLimit > 0 ? (double)(budgetSpent / budgetLimit) : 0;

            _budgetSubtitleLabel.Text = $"{MoneyDisplay.Format(currency, budgetSpent)} spent of {MoneyDisplay.Format(currency, budgetLimit)}";
            _budgetProgress.Progress = Math.Min(progress, 1.0);
            _budgetProgress.ProgressColor = progress > 1 ? AppColors.Danger : AppColors.Accent;
            _plannedLabel.Text = $"Planned: {MoneyDisplay.Format(currency, planned)}";
            _spentLabel.Text = $"Spent: {MoneyDisplay.Format(currency, budgetSpent)}";
            _availableLabel.Text = $"Available: {MoneyDisplay.Format(currency, available)}";
            _availableLabel.TextColor = available >= 0 ? AppColors.Success : AppColors.Danger;

            // Reserves
            var savingsState = savings.State;
            _savingsLabel.Text = MoneyDisplay.Format(currency, savingsState.Available);
            _emergencyLabel.Text = MoneyDisplay.Format(currency, savingsState.EmergencyAvailable);
            _totalReservedLabel.Text = MoneyDisplay.Format(currency, savingsState.Available + savingsState.EmergencyAvailable);

            // Transactions
            _recentTxList.Children.Clear();
            if (snapshot.RecentTransactions.Count == 0)
            {
                _recentTxList.Children.Add(CreateEmptyPlaceholder("No transactions yet."));
            }
            else
            {
                foreach (var tx in snapshot.RecentTransactions)
                {
                    bool isIncome = tx.Type == TransactionType.Income;
                    var prefix = isIncome ? "+" : "-";
                    var amount = MoneyDisplay.Format(currency, tx.Amount);

                    var row = new Grid
                    {
                        ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                        Padding = new Thickness(12, 10),
                    };

                    var info = new VerticalStackLayout
                    {
                        Children =
                        {
                            new Label { Text = tx.Description, FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark, LineBreakMode = LineBreakMode.TailTruncation },
                            new Label { Text = tx.OccurredAtUtc.ToString("dd MMM"), FontSize = 11, TextColor = AppColors.Muted, FontFamily = "InterRegular" }
                        }
                    };

                    var amountLabel = new Label
                    {
                        Text = $"{prefix}{amount}",
                        FontFamily = "InterBold",
                        FontSize = 14,
                        TextColor = isIncome ? AppColors.Success : AppColors.Dark,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.End
                    };

                    row.Add(info, 0, 0);
                    row.Add(amountLabel, 1, 0);

                    _recentTxList.Children.Add(new Border
                    {
                        BackgroundColor = AppColors.SurfaceDim,
                        StrokeShape = new RoundRectangle { CornerRadius = 10 },
                        Stroke = new SolidColorBrush(AppColors.Border),
                        StrokeThickness = 1,
                        Content = row
                    });
                }
            }

            // Score events
            _scoreEventList.Children.Clear();
            if (scoreEvents.Count == 0)
            {
                _scoreEventList.Children.Add(CreateEmptyPlaceholder("No score activity yet."));
            }
            else
            {
                foreach (var evt in scoreEvents)
                {
                    var pointsText = (evt.Points >= 0 ? "+" : "") + evt.Points.ToString();
                    var pointsColor = evt.Points >= 0 ? AppColors.Success : AppColors.Danger;

                    var row = new Grid
                    {
                        ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                        Padding = new Thickness(12, 10),
                    };

                    var info = new VerticalStackLayout
                    {
                        Children =
                        {
                            new Label { Text = evt.Description, FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark, LineBreakMode = LineBreakMode.TailTruncation },
                            new Label { Text = $"{evt.Reason} \u00b7 {evt.CreatedAtUtc:dd MMM, HH:mm}", FontSize = 11, TextColor = AppColors.Muted, FontFamily = "InterRegular" }
                        }
                    };

                    var pointsLabel = new Label
                    {
                        Text = pointsText,
                        FontFamily = "InterBold",
                        FontSize = 14,
                        TextColor = pointsColor,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.End
                    };

                    row.Add(info, 0, 0);
                    row.Add(pointsLabel, 1, 0);

                    _scoreEventList.Children.Add(new Border
                    {
                        BackgroundColor = AppColors.SurfaceDim,
                        StrokeShape = new RoundRectangle { CornerRadius = 10 },
                        Stroke = new SolidColorBrush(AppColors.Border),
                        StrokeThickness = 1,
                        Content = row
                    });
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private static Border CreateReserveRow(string label, Label valueLabel)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Padding = new Thickness(12, 10),
        };
        grid.Add(new Label { Text = label, FontSize = 13, TextColor = AppColors.Muted, FontFamily = "InterRegular", VerticalOptions = LayoutOptions.Center }, 0, 0);
        grid.Add(valueLabel, 1, 0);

        return new Border
        {
            BackgroundColor = AppColors.SurfaceDim,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Stroke = Brush.Transparent,
            Content = grid
        };
    }

    private static Border WrapBadge(Label content, Color bgColor, Color dotColor)
    {
        var dot = new BoxView { WidthRequest = 8, HeightRequest = 8, Color = dotColor, CornerRadius = 4, VerticalOptions = LayoutOptions.Center };
        return new Border
        {
            BackgroundColor = bgColor,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Stroke = Brush.Transparent,
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 0, 6, 6),
            Content = new HorizontalStackLayout { Spacing = 6, Children = { dot, content } }
        };
    }

    private static Label CreateEmptyPlaceholder(string text) =>
        new() { Text = text, FontFamily = "InterRegular", TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 20) };
}
