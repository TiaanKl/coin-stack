using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class WeeklyRecapMobilePage : ContentPage
{
    private readonly IMobileFinanceService _financeService;
    private readonly VerticalStackLayout _recapList;

    public WeeklyRecapMobilePage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Weekly Recap";
        BackgroundColor = AppColors.Background;

        _recapList = new VerticalStackLayout { Spacing = 12 };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 8,
                Children =
                {
                    new Label { Text = "Weekly Recap", FontFamily = "SpaceGroteskBold", FontSize = 24, TextColor = AppColors.Dark },
                    new Label { Text = "Your financial week at a glance", FontSize = 13, TextColor = AppColors.Muted, FontFamily = "SpaceGroteskRegular" },
                    _recapList
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
            var recaps = await _financeService.GetWeeklyRecapsAsync();
            _recapList.Children.Clear();

            if (recaps.Count == 0)
            {
                _recapList.Children.Add(new Border
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
                            new Label { Text = AppIcons.GlyphCalendarWeek, FontFamily = "FontAwesomeSolid", FontSize = 32, HorizontalOptions = LayoutOptions.Center, TextColor = AppColors.Muted },
                            new Label { Text = "No Recaps Yet", FontFamily = "SpaceGroteskBold", FontSize = 16, TextColor = AppColors.Dark, HorizontalTextAlignment = TextAlignment.Center },
                            new Label { Text = "Weekly recaps will appear here at the end of each week.", FontSize = 13, TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, FontFamily = "SpaceGroteskRegular" }
                        }
                    }
                });
                return;
            }

            foreach (var r in recaps)
            {
                _recapList.Children.Add(BuildRecapCard(r));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private static Border BuildRecapCard(CoinStack.Data.Entities.WeeklyRecap r)
    {
        var weekLabel = $"Week {r.WeekNumber}, {r.Year}";

        var statsGrid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            ],
            RowDefinitions = [new RowDefinition(GridLength.Auto)],
            ColumnSpacing = 8
        };

        statsGrid.Add(CreateStatCell("Income", r.TotalIncome.ToString("C0"), AppColors.Success), 0, 0);
        statsGrid.Add(CreateStatCell("Spent", r.TotalSpent.ToString("C0"), AppColors.Danger), 1, 0);
        statsGrid.Add(CreateStatCell("Saved", r.TotalSaved.ToString("C0"), r.TotalSaved >= 0 ? AppColors.Success : AppColors.Danger), 2, 0);
        statsGrid.Add(CreateStatCell("Points", r.PointsEarned.ToString(), AppColors.Accent), 3, 0);

        var badges = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start
        };

        badges.Children.Add(CreateBadge($"{r.ChallengesCompleted} challenges", AppIcons.GlyphBolt, Color.FromArgb("#F0F1FE"), Color.FromArgb("#6577F3")));
        badges.Children.Add(CreateBadge($"{r.ReflectionsCompleted} reflections", AppIcons.GlyphBookOpen, Color.FromArgb("#F5F3FF"), Color.FromArgb("#7C3AED")));
        badges.Children.Add(CreateBadge($"{r.StreakDays} day streak", AppIcons.GlyphFire, Color.FromArgb("#FFFBEB"), Color.FromArgb("#D97706")));
        if (!string.IsNullOrEmpty(r.TopCategory))
            badges.Children.Add(CreateBadge($"Top: {r.TopCategory}", AppIcons.GlyphTags, Color.FromArgb("#F3F4F6"), Color.FromArgb("#4B5563")));

        var card = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label { Text = AppIcons.GlyphCalendarWeek, FontFamily = "FontAwesomeSolid", FontSize = 16, TextColor = AppColors.Accent, VerticalOptions = LayoutOptions.Center },
                        new Label { Text = weekLabel, FontFamily = "SpaceGroteskBold", FontSize = 16, TextColor = AppColors.Dark, VerticalOptions = LayoutOptions.Center }
                    }
                },
                statsGrid,
                badges
            }
        };

        if (!string.IsNullOrEmpty(r.InsightMessage))
        {
            card.Children.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#F0F1FE"),
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Stroke = Brush.Transparent,
                Padding = new Thickness(12),
                Content = new Label
                {
                    Text = r.InsightMessage,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#4338CA"),
                    FontFamily = "SpaceGroteskRegular"
                }
            });
        }

        return new Border
        {
            BackgroundColor = AppColors.Surface,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Stroke = new SolidColorBrush(AppColors.Border),
            StrokeThickness = 1,
            Padding = new Thickness(16),
            Content = card
        };
    }

    private static Border CreateStatCell(string label, string value, Color valueColor)
    {
        return new Border
        {
            BackgroundColor = Color.FromArgb("#F8F9FA"),
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Stroke = Brush.Transparent,
            Padding = new Thickness(8),
            Content = new VerticalStackLayout
            {
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = label, FontSize = 10, TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, FontFamily = "SpaceGroteskRegular" },
                    new Label { Text = value, FontSize = 16, FontFamily = "SpaceGroteskBold", TextColor = valueColor, HorizontalTextAlignment = TextAlignment.Center }
                }
            }
        };
    }

    private static Border CreateBadge(string text, string iconGlyph, Color bgColor, Color textColor)
    {
        return new Border
        {
            BackgroundColor = bgColor,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Stroke = Brush.Transparent,
            Padding = new Thickness(10, 4),
            Margin = new Thickness(0, 0, 6, 6),
            Content = new HorizontalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label { Text = iconGlyph, FontFamily = "FontAwesomeSolid", FontSize = 10, TextColor = textColor, VerticalOptions = LayoutOptions.Center },
                    new Label { Text = text, FontSize = 11, FontFamily = "SpaceGroteskBold", TextColor = textColor }
                }
            }
        };
    }
}
