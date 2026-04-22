using CoinStack.Mobile.Core;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

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

        _incomeLabel = new Label { FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark };
        _expenseLabel = new Label { FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark };
        _netLabel = new Label { FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark };
        _savingsLabel = new Label { FontSize = 13, FontFamily = "InterRegular", TextColor = AppColors.Muted };
        _fallbackLabel = new Label { FontSize = 12, FontFamily = "InterRegular", TextColor = AppColors.Muted };
        _debtCountLabel = new Label { FontSize = 14, FontFamily = "InterRegular", TextColor = AppColors.Dark };
        _debtOutstandingLabel = new Label { FontSize = 14, FontFamily = "InterRegular", TextColor = AppColors.Dark };
        _debtMonthlyLabel = new Label { FontSize = 14, FontFamily = "InterRegular", TextColor = AppColors.Dark };
        _bucketList = new VerticalStackLayout { Spacing = 6 };
        _dailyList = new VerticalStackLayout { Spacing = 4 };

        // ── Cashflow Card ──
        var cashflowCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Cashflow Overview", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                CreateStatRow(AppIcons.GlyphArrowTrendUp, _incomeLabel),
                CreateStatRow(AppIcons.GlyphArrowTrendUp, _expenseLabel),
                new BoxView { HeightRequest = 1, Color = AppColors.Border },
                CreateStatRow(AppIcons.GlyphReceipt, _netLabel),
                CreateStatRow(AppIcons.GlyphPiggyBank, _savingsLabel),
                _fallbackLabel
            }
        });

        // ── Debt Card ──
        var debtCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Debt Summary", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                CreateStatRow(AppIcons.GlyphReceipt, _debtCountLabel),
                CreateStatRow(AppIcons.GlyphReceipt, _debtOutstandingLabel),
                CreateStatRow(AppIcons.GlyphReceipt, _debtMonthlyLabel)
            }
        });

        // ── Bucket Spending Card ──
        var bucketCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Top Bucket Spending", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _bucketList
            }
        });

        // ── Daily Net Card ──
        var dailyCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Last 14 Days Net", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _dailyList
            }
        });

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children = { cashflowCard, debtCard, bucketCard, dailyCard }
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
            _incomeLabel.TextColor = AppColors.Success;
            _expenseLabel.Text = $"Expenses (Period): {MoneyDisplay.Format(settings.Currency, report.PeriodExpense)}";
            _expenseLabel.TextColor = AppColors.Danger;
            _netLabel.Text = $"Net Cashflow: {MoneyDisplay.Format(settings.Currency, report.PeriodNet)}";
            _netLabel.TextColor = report.PeriodNet >= 0 ? AppColors.Success : AppColors.Danger;
            _savingsLabel.Text = $"Savings Available: {MoneyDisplay.Format(settings.Currency, report.SavingsAvailable)}";
            _fallbackLabel.Text = $"Fallback Used This Month: {MoneyDisplay.Format(settings.Currency, report.FallbackUsedThisMonth)}";
            _debtCountLabel.Text = $"Open Debts: {report.OpenDebtCount}";
            _debtOutstandingLabel.Text = $"Debt Outstanding: {MoneyDisplay.Format(settings.Currency, report.TotalDebtOutstanding)}";
            _debtMonthlyLabel.Text = $"Debt Monthly Payments: {MoneyDisplay.Format(settings.Currency, report.TotalDebtMonthlyPayment)}";

            _bucketList.Children.Clear();
            if (report.TopBucketSpending.Count == 0)
            {
                _bucketList.Children.Add(CreateEmptyPlaceholder("No expense data for this period."));
            }
            else
            {
                foreach (var row in report.TopBucketSpending)
                {
                    var progress = row.Allocated > 0 ? (double)(row.Spent / row.Allocated) : 0;
                    var bucketRow = new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children =
                        {
                            new Grid
                            {
                                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                                Children =
                                {
                                    new Label { Text = row.Name, FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark },
                                }
                            },
                            new ProgressBar { Progress = Math.Min(progress, 1.0), ProgressColor = progress > 1 ? AppColors.Danger : AppColors.Accent, HeightRequest = 6 },
                            new Label
                            {
                                Text = $"{MoneyDisplay.Format(settings.Currency, row.Spent)} / {MoneyDisplay.Format(settings.Currency, row.Allocated)}",
                                FontSize = 12, FontFamily = "InterRegular", TextColor = AppColors.Muted
                            }
                        }
                    };

                    var amountLabel = new Label
                    {
                        Text = MoneyDisplay.Format(settings.Currency, row.Spent),
                        FontFamily = "InterBold",
                        FontSize = 14,
                        TextColor = progress > 1 ? AppColors.Danger : AppColors.Dark,
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Center
                    };
                    ((Grid)bucketRow.Children[0]).Add(amountLabel, 1, 0);

                    _bucketList.Children.Add(new Border
                    {
                        BackgroundColor = AppColors.SurfaceDim,
                        StrokeShape = new RoundRectangle { CornerRadius = 10 },
                        Stroke = Brush.Transparent,
                        Padding = new Thickness(12, 10),
                        Content = bucketRow
                    });
                }
            }

            _dailyList.Children.Clear();
            foreach (var row in report.DailyNet14Days)
            {
                var netColor = row.Net >= 0 ? AppColors.Success : AppColors.Danger;

                var dayRow = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(60)),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Padding = new Thickness(12, 8),
                };

                var dateLabel = new Label { Text = row.Date.ToString("dd MMM"), FontFamily = "InterBold", FontSize = 13, TextColor = AppColors.Dark, VerticalOptions = LayoutOptions.Center };
                var flowLabel = new Label
                {
                    Text = $"In {MoneyDisplay.Format(settings.Currency, row.Income)} · Out {MoneyDisplay.Format(settings.Currency, row.Expense)}",
                    FontSize = 11,
                    FontFamily = "InterRegular",
                    TextColor = AppColors.Muted,
                    VerticalOptions = LayoutOptions.Center
                };
                var netLabel = new Label
                {
                    Text = MoneyDisplay.Format(settings.Currency, row.Net),
                    FontFamily = "InterBold",
                    FontSize = 13,
                    TextColor = netColor,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Center
                };

                dayRow.Add(dateLabel, 0, 0);
                dayRow.Add(flowLabel, 1, 0);
                dayRow.Add(netLabel, 2, 0);

                _dailyList.Children.Add(new Border
                {
                    BackgroundColor = AppColors.SurfaceDim,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Stroke = Brush.Transparent,
                    Content = dayRow
                });
            }
        }
        catch (Exception ex)
        {
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

    private static Grid CreateStatRow(string iconGlyph, Label valueLabel)
    {
        var icon = AppIcons.CreateLabel(iconGlyph, AppColors.Muted, 16);
        icon.VerticalOptions = LayoutOptions.Center;
        valueLabel.VerticalOptions = LayoutOptions.Center;

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(28)), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8,
        };
        grid.Add(icon, 0, 0);
        grid.Add(valueLabel, 1, 0);
        return grid;
    }

    private static Label CreateEmptyPlaceholder(string text) =>
        new() { Text = text, FontFamily = "InterRegular", TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 16) };
}
