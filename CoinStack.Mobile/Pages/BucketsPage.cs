using CoinStack.Mobile.Core;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class BucketsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Entry _nameEntry;
    private readonly Entry _amountEntry;
    private readonly Switch _isSavingsSwitch;
    private readonly VerticalStackLayout _list;

    public BucketsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Buckets";

        _nameEntry = new Entry { Placeholder = "Bucket name" };
        _amountEntry = new Entry { Placeholder = "Allocated amount", Keyboard = Keyboard.Numeric };
        _isSavingsSwitch = new Switch();

        var addButton = new Button { Text = "Add Bucket" };
        addButton.Clicked += async (_, _) => await AddBucketAsync();

        _list = new VerticalStackLayout { Spacing = 8 };

        // ── Add Bucket Card ──
        var formCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = "New Bucket", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _nameEntry,
                _amountEntry,
                new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    Children =
                    {
                        new Label { Text = "Savings bucket", FontFamily = "InterRegular", FontSize = 14, TextColor = AppColors.Dark, VerticalOptions = LayoutOptions.Center },
                    }
                },
                addButton
            }
        });
        ((Grid)((VerticalStackLayout)formCard.Content).Children[3]).Add(_isSavingsSwitch, 1, 0);

        // ── Buckets List Card ──
        var listCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Current Buckets", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _list
            }
        });

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children = { formCard, listCard }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadBucketsAsync();
    }

    private async Task AddBucketAsync()
    {
        if (!decimal.TryParse(_amountEntry.Text, out var amount))
        {
            await DisplayAlertAsync("Validation", "Enter a valid allocated amount.", "OK");
            return;
        }

        try
        {
            await _financeService.AddBucketAsync(_nameEntry.Text ?? string.Empty, amount, _isSavingsSwitch.IsToggled);
            _nameEntry.Text = string.Empty;
            _amountEntry.Text = string.Empty;
            _isSavingsSwitch.IsToggled = false;
            await LoadBucketsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async Task LoadBucketsAsync()
    {
        try
        {
            var settings = await _financeService.GetSettingsAsync();
            var buckets = await _financeService.GetBucketsAsync();

            _list.Children.Clear();
            if (buckets.Count == 0)
            {
                _list.Children.Add(new Label { Text = "No buckets yet.", FontFamily = "InterRegular", TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 16) });
                return;
            }

            foreach (var bucket in buckets)
            {
                var typeBadge = new Border
                {
                    BackgroundColor = bucket.IsSavings ? AppColors.BgSuccess : AppColors.SurfaceContainer,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Stroke = Brush.Transparent,
                    Padding = new Thickness(8, 4),
                    Content = new Label { Text = bucket.IsSavings ? "Savings" : "Spending", FontSize = 11, FontFamily = "InterBold", TextColor = bucket.IsSavings ? AppColors.Success : AppColors.Muted }
                };

                var row = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
                    ColumnSpacing = 8,
                    RowSpacing = 4,
                    Padding = new Thickness(12, 10)
                };
                row.Add(new Label { Text = bucket.Name, FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark }, 0, 0);
                row.Add(new Label { Text = MoneyDisplay.Format(settings.Currency, bucket.AllocatedAmount), FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark, HorizontalOptions = LayoutOptions.End }, 1, 0);
                row.Add(typeBadge, 0, 1);

                _list.Children.Add(new Border
                {
                    BackgroundColor = AppColors.SurfaceDim,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Stroke = Brush.Transparent,
                    Content = row
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
}
