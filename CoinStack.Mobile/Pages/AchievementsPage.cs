using CoinStack.Data.Entities;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class AchievementsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;
    private readonly VerticalStackLayout _content;

    public AchievementsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Achievements";
        BackgroundColor = AppColors.Background;

        _content = new VerticalStackLayout
        {
            Padding = new Thickness(20),
            Spacing = 16
        };

        Content = new ScrollView { Content = _content };
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
            var levelInfo = await _financeService.GetLevelInfoAsync();
            var achievements = await _financeService.GetAchievementsAsync();

            _content.Children.Clear();

            _content.Children.Add(new Label { Text = "Achievements", FontFamily = "SpaceGroteskBold", FontSize = 24, TextColor = AppColors.Dark });

            // Level card
            var xpProgress = levelInfo.XpForNextLevel > 0 ? (double)levelInfo.CurrentXp / levelInfo.XpForNextLevel : 0;
            var levelCard = new Border
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
                        new HorizontalStackLayout
                        {
                            Spacing = 12,
                            Children =
                            {
                                new Border
                                {
                                    BackgroundColor = Color.FromArgb("#6577F3"),
                                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                                    Stroke = Colors.Transparent,
                                    WidthRequest = 48,
                                    HeightRequest = 48,
                                    Padding = 0,
                                    Content = new Label
                                    {
                                        Text = levelInfo.Level.ToString(),
                                        FontSize = 20,
                                        FontFamily = "SpaceGroteskBold",
                                        TextColor = Colors.White,
                                        HorizontalTextAlignment = TextAlignment.Center,
                                        VerticalTextAlignment = TextAlignment.Center
                                    }
                                },
                                new VerticalStackLayout
                                {
                                    Spacing = 2,
                                    VerticalOptions = LayoutOptions.Center,
                                    Children =
                                    {
                                        new Label { Text = levelInfo.LevelName, FontFamily = "SpaceGroteskBold", FontSize = 16, TextColor = AppColors.Dark },
                                        new Label { Text = $"{levelInfo.CurrentXp} / {levelInfo.XpForNextLevel} XP to Level {levelInfo.Level + 1}", FontSize = 12, TextColor = AppColors.Muted, FontFamily = "SpaceGroteskRegular" }
                                    }
                                }
                            }
                        },
                        new ProgressBar { Progress = xpProgress, ProgressColor = Color.FromArgb("#6577F3") }
                    }
                }
            };
            _content.Children.Add(levelCard);

            // Group achievements by category
            var grouped = achievements.GroupBy(a => a.Category).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                var categoryName = group.Key.ToString().ToUpperInvariant();
                _content.Children.Add(new Label
                {
                    Text = categoryName,
                    FontSize = 12,
                    FontFamily = "SpaceGroteskBold",
                    TextColor = AppColors.Muted,
                    Margin = new Thickness(0, 8, 0, 0)
                });

                foreach (var achievement in group)
                {
                    var unlocked = achievement.IsUnlocked;

                    var iconContent = new Border
                    {
                        BackgroundColor = unlocked ? Color.FromArgb("#DDE0FA") : AppColors.Background,
                        StrokeShape = new RoundRectangle { CornerRadius = 10 },
                        Stroke = Colors.Transparent,
                        WidthRequest = 40,
                        HeightRequest = 40,
                        Padding = 0,
                        Content = new Label
                        {
                            Text = unlocked ? AppIcons.GlyphCheck : AppIcons.GlyphTrophy,
                            FontFamily = "FontAwesomeSolid",
                            FontSize = 16,
                            HorizontalTextAlignment = TextAlignment.Center,
                            VerticalTextAlignment = TextAlignment.Center,
                            TextColor = unlocked ? Color.FromArgb("#6577F3") : AppColors.Muted
                        }
                    };

                    var xpLabel = new Label
                    {
                        Text = $"+{achievement.XpReward}XP",
                        FontSize = 12,
                        FontFamily = "SpaceGroteskBold",
                        TextColor = unlocked ? Color.FromArgb("#6577F3") : AppColors.Muted,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.End
                    };

                    var grid = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(new GridLength(48)),
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        ColumnSpacing = 12
                    };

                    var textStack = new VerticalStackLayout
                    {
                        Spacing = 2,
                        VerticalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new Label { Text = achievement.Title, FontFamily = "SpaceGroteskBold", FontSize = 14, TextColor = AppColors.Dark },
                            new Label { Text = achievement.Description, FontSize = 11, TextColor = AppColors.Muted, FontFamily = "SpaceGroteskRegular" }
                        }
                    };

                    if (unlocked && achievement.UnlockedAtUtc.HasValue)
                    {
                        textStack.Children.Add(new Label { Text = $"Unlocked {achievement.UnlockedAtUtc.Value:dd MMM yyyy}", FontSize = 10, TextColor = Color.FromArgb("#6577F3"), FontFamily = "SpaceGroteskRegular" });
                    }

                    Grid.SetColumn(iconContent, 0);
                    Grid.SetColumn(textStack, 1);
                    Grid.SetColumn(xpLabel, 2);
                    grid.Children.Add(iconContent);
                    grid.Children.Add(textStack);
                    grid.Children.Add(xpLabel);

                    var card = new Border
                    {
                        BackgroundColor = unlocked ? Color.FromArgb("#F0F1FE") : AppColors.Surface,
                        StrokeShape = new RoundRectangle { CornerRadius = 14 },
                        Stroke = new SolidColorBrush(unlocked ? Color.FromArgb("#6577F3") : AppColors.Border),
                        StrokeThickness = 1,
                        Padding = new Thickness(14),
                        Opacity = unlocked ? 1 : 0.6,
                        Content = grid
                    };

                    _content.Children.Add(card);
                }
            }

            if (achievements.Count == 0)
            {
                _content.Children.Add(new Label
                {
                    Text = "No achievements seeded yet. Use the web app to seed default achievements.",
                    FontSize = 14,
                    TextColor = AppColors.Muted,
                    FontFamily = "SpaceGroteskRegular",
                    HorizontalTextAlignment = TextAlignment.Center
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
