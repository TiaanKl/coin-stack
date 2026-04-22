using CoinStack.Mobile.Core;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class IncomePage : ContentPage
{
    private readonly IMobileFinanceService _financeService;
    private readonly Label _mtdLabel;
    private readonly Label _ytdLabel;
    private readonly Label _avgLabel;
    private readonly VerticalStackLayout _depositsList;

    public IncomePage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Income";

        _mtdLabel = new Label { Text = "$0.00", FontFamily = "InterBold", FontSize = 28, TextColor = AppColors.Dark };
        _ytdLabel = new Label { Text = "$0.00", FontFamily = "InterBold", FontSize = 18, TextColor = AppColors.Success };
        _avgLabel = new Label { Text = "$0.00", FontFamily = "InterRegular", FontSize = 14, TextColor = AppColors.Muted };
        _depositsList = new VerticalStackLayout { Spacing = 8 };

        var summaryCard = new Border
        {
            BackgroundColor = AppColors.Surface,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = new SolidColorBrush(AppColors.Border),
            StrokeThickness = 1,
            Padding = new Thickness(20),
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Month to Date", FontSize = 13, TextColor = AppColors.Muted, FontFamily = "InterRegular" },
                    _mtdLabel,
                    new BoxView { HeightRequest = 1, Color = AppColors.Border },
                    CreateStatRow("Year to Date", _ytdLabel),
                    CreateStatRow("Monthly Average", _avgLabel),
                }
            }
        };

        var depositsCard = new Border
        {
            BackgroundColor = AppColors.Surface,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = new SolidColorBrush(AppColors.Border),
            StrokeThickness = 1,
            Padding = new Thickness(20),
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Recent Deposits", FontFamily = "InterBold", FontSize = 18, TextColor = AppColors.Dark },
                    _depositsList
                }
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new Label { Text = "Income Streams", FontFamily = "InterBold", FontSize = 24, TextColor = AppColors.Dark },
                    summaryCard,
                    depositsCard
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
            var snapshot = await _financeService.GetIncomeSnapshotAsync();

            _mtdLabel.Text = MoneyDisplay.Format(settings.Currency, snapshot.MonthToDate);
            _ytdLabel.Text = MoneyDisplay.Format(settings.Currency, snapshot.YearToDate);
            _avgLabel.Text = $"{MoneyDisplay.Format(settings.Currency, snapshot.MonthlyAverage)} / month";

            _depositsList.Children.Clear();
            if (snapshot.RecentDeposits.Count == 0)
            {
                _depositsList.Children.Add(new Label { Text = "No income recorded yet.", FontFamily = "InterRegular", FontSize = 14, TextColor = AppColors.Muted });
                return;
            }

            foreach (var tx in snapshot.RecentDeposits)
            {
                var row = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    Padding = new Thickness(0, 4)
                };
                var info = new VerticalStackLayout
                {
                    Children =
                    {
                        new Label { Text = tx.Description, FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark },
                        new Label { Text = tx.OccurredAtUtc.ToString("dd MMM yyyy"), FontSize = 12, TextColor = AppColors.Muted, FontFamily = "InterRegular" }
                    }
                };
                Grid.SetColumn(info, 0);
                var amount = new Label
                {
                    Text = $"+{MoneyDisplay.Format(settings.Currency, tx.Amount)}",
                    FontFamily = "InterBold",
                    FontSize = 14,
                    TextColor = AppColors.Success,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.End
                };
                Grid.SetColumn(amount, 1);
                row.Children.Add(info);
                row.Children.Add(amount);
                _depositsList.Children.Add(row);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private static View CreateStatRow(string label, Label valueLabel)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
        };
        var lbl = new Label { Text = label, FontSize = 14, TextColor = AppColors.Muted, FontFamily = "InterRegular", VerticalOptions = LayoutOptions.Center };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(valueLabel, 1);
        grid.Children.Add(lbl);
        grid.Children.Add(valueLabel);
        return grid;
    }
}
