using CoinStack.Mobile.Core;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;

namespace CoinStack.Mobile.Pages;

public sealed class FallbackHistoryPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;
    private readonly VerticalStackLayout _eventsList;

    public FallbackHistoryPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Fallback History";
        BackgroundColor = AppColors.Background;

        _eventsList = new VerticalStackLayout { Spacing = 8 };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    CreateHeader(),
                    _eventsList
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
            var snapshot = await _financeService.GetSavingsSnapshotAsync();
            _eventsList.Children.Clear();

            if (snapshot.FallbackEvents.Count == 0)
            {
                _eventsList.Children.Add(new Border
                {
                    BackgroundColor = AppColors.Surface,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                    Stroke = new SolidColorBrush(AppColors.Border),
                    StrokeThickness = 1,
                    Padding = new Thickness(24),
                    Content = new Label
                    {
                        Text = "No fallback events recorded yet.",
                        FontSize = 14,
                        TextColor = AppColors.Muted,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                });
                return;
            }

            foreach (var evt in snapshot.FallbackEvents)
            {
                var card = new Border
                {
                    BackgroundColor = AppColors.Surface,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                    Stroke = new SolidColorBrush(AppColors.Border),
                    StrokeThickness = 1,
                    Padding = new Thickness(16),
                    Content = new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children =
                        {
                            new Label
                            {
                                Text = $"{evt.OccurredAtUtc:dd MMM yyyy}",
                                FontSize = 14,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = AppColors.Dark
                            },
                            new Label
                            {
                                Text = $"Amount: {MoneyDisplay.Format("USD", evt.AmountUsed)}",
                                FontSize = 14,
                                TextColor = AppColors.Danger
                            },
                            new Label
                            {
                                Text = string.IsNullOrWhiteSpace(evt.Reason) ? "No reason provided" : evt.Reason,
                                FontSize = 13,
                                TextColor = AppColors.Muted
                            }
                        }
                    }
                };
                _eventsList.Children.Add(card);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private static View CreateHeader()
    {
        var icon = AppIcons.CreateLabel(AppIcons.GlyphClockRotateLeft, AppColors.Accent, 32);

        return new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                icon,
                new Label
                {
                    Text = "Fallback History",
                    FontSize = 24,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = AppColors.Dark
                },
                new Label
                {
                    Text = "Track when your savings fallback coverage was used.",
                    FontSize = 14,
                    TextColor = AppColors.Muted
                }
            }
        };
    }
}
