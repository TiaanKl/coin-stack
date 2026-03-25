using CoinStack.Mobile.Core;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class WaitlistPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;
    private readonly VerticalStackLayout _itemsList;

    public WaitlistPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Waitlist";
        BackgroundColor = AppColors.Background;

        _itemsList = new VerticalStackLayout { Spacing = 10 };

        var addButton = new Button
        {
            Text = "Add Item to Waitlist",
            BackgroundColor = AppColors.Dark,
            TextColor = AppColors.Surface,
            CornerRadius = 24,
            HeightRequest = 48,
            FontFamily = "SpaceGroteskBold",
            FontSize = 14
        };
        addButton.Clicked += OnAddClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new Label { Text = "Impulse Waitlist", FontFamily = "SpaceGroteskBold", FontSize = 24, TextColor = AppColors.Dark },
                    new Label { Text = "Let time and your finances decide \u2014 not impulse.", FontSize = 14, TextColor = AppColors.Muted, FontFamily = "SpaceGroteskRegular" },
                    addButton,
                    _itemsList
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
            var items = await _financeService.GetWaitlistItemsAsync();
            _itemsList.Children.Clear();

            if (items.Count == 0)
            {
                _itemsList.Children.Add(new Border
                {
                    BackgroundColor = AppColors.Surface,
                    StrokeShape = new RoundRectangle { CornerRadius = 16 },
                    Stroke = new SolidColorBrush(AppColors.Border),
                    StrokeThickness = 1,
                    Padding = new Thickness(24),
                    Content = new VerticalStackLayout
                    {
                        Spacing = 8,
                        HorizontalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new Label { Text = AppIcons.GlyphHourglass, FontFamily = "FontAwesomeSolid", FontSize = 32, HorizontalOptions = LayoutOptions.Center, TextColor = AppColors.Muted },
                            new Label { Text = "No impulse items yet", FontFamily = "SpaceGroteskBold", FontSize = 16, TextColor = AppColors.Dark, HorizontalTextAlignment = TextAlignment.Center },
                            new Label { Text = "Add items you're tempted to buy and let the cool-off period help you decide.", FontSize = 13, TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, FontFamily = "SpaceGroteskRegular" }
                        }
                    }
                });
                return;
            }

            foreach (var item in items)
            {
                var now = DateTime.UtcNow;
                var isUnlocked = item.CoolOffUntil <= now || item.IsUnlocked;
                var isPurchased = item.IsPurchased;

                var statusColor = isPurchased ? AppColors.Muted : isUnlocked ? AppColors.Success : Color.FromArgb("#D97706");
                var statusText = isPurchased ? "Purchased" : isUnlocked ? "Unlocked" : $"Cool-off: {(item.CoolOffUntil - now).Days}d left";

                var statusBadge = new Border
                {
                    BackgroundColor = isPurchased ? Color.FromArgb("#F3F4F6") : isUnlocked ? Color.FromArgb("#ECFDF5") : Color.FromArgb("#FFFBEB"),
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Stroke = Colors.Transparent,
                    Padding = new Thickness(8, 4),
                    Content = new Label { Text = statusText, FontSize = 11, FontFamily = "SpaceGroteskBold", TextColor = statusColor }
                };

                var nameRow = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
                };
                var nameLabel = new Label { Text = item.Name, FontFamily = "SpaceGroteskBold", FontSize = 15, TextColor = AppColors.Dark };
                var costLabel = new Label { Text = MoneyDisplay.Format(settings.Currency, item.EstimatedCost), FontFamily = "SpaceGroteskBold", FontSize = 15, TextColor = AppColors.Dark, HorizontalOptions = LayoutOptions.End };
                Grid.SetColumn(nameLabel, 0);
                Grid.SetColumn(costLabel, 1);
                nameRow.Children.Add(nameLabel);
                nameRow.Children.Add(costLabel);

                var actionRow = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End };

                if (!isPurchased && isUnlocked)
                {
                    var purchaseBtn = new Button
                    {
                        Text = "Mark Purchased",
                        BackgroundColor = AppColors.Success,
                        TextColor = Colors.White,
                        CornerRadius = 12,
                        HeightRequest = 32,
                        FontSize = 12,
                        Padding = new Thickness(12, 0),
                        FontFamily = "SpaceGroteskBold"
                    };
                    var itemId = item.Id;
                    purchaseBtn.Clicked += async (_, _) =>
                    {
                        await _financeService.MarkWaitlistItemPurchasedAsync(itemId);
                        await LoadAsync();
                    };
                    actionRow.Children.Add(purchaseBtn);
                }

                var deleteBtn = new Label
                {
                    Text = AppIcons.GlyphTrash,
                    FontFamily = "FontAwesomeSolid",
                    FontSize = 16,
                    TextColor = AppColors.Danger,
                    VerticalOptions = LayoutOptions.Center
                };
                var deleteTap = new TapGestureRecognizer();
                var deleteId = item.Id;
                deleteTap.Tapped += async (_, _) =>
                {
                    var confirmed = await DisplayAlertAsync("Delete", $"Remove '{item.Name}' from waitlist?", "Delete", "Cancel");
                    if (confirmed)
                    {
                        await _financeService.DeleteWaitlistItemAsync(deleteId);
                        await LoadAsync();
                    }
                };
                deleteBtn.GestureRecognizers.Add(deleteTap);
                actionRow.Children.Add(deleteBtn);

                var card = new Border
                {
                    BackgroundColor = AppColors.Surface,
                    StrokeShape = new RoundRectangle { CornerRadius = 16 },
                    Stroke = new SolidColorBrush(AppColors.Border),
                    StrokeThickness = 1,
                    Padding = new Thickness(16),
                    Opacity = isPurchased ? 0.6 : 1.0,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            nameRow,
                            statusBadge,
                            actionRow
                        }
                    }
                };

                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    ((VerticalStackLayout)card.Content).Children.Insert(1, new Label { Text = item.Description, FontSize = 13, TextColor = AppColors.Muted, FontFamily = "SpaceGroteskRegular" });
                }

                _itemsList.Children.Add(card);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Add to Waitlist", "What do you want to buy?", "Next", "Cancel");
        if (string.IsNullOrWhiteSpace(name)) return;

        var costStr = await DisplayPromptAsync("Estimated Cost", "How much does it cost?", "Add", "Cancel", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(costStr) || !decimal.TryParse(costStr, out var cost)) return;

        try
        {
            await _financeService.AddWaitlistItemAsync(name, cost);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
