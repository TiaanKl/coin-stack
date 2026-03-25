using CoinStack.Data.Entities;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class ChallengesPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;
    private readonly VerticalStackLayout _content;

    public ChallengesPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Challenges";
        BackgroundColor = AppColors.Background;

        _content = new VerticalStackLayout
        {
            Padding = new Thickness(20),
            Spacing = 12
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
            var challenges = await _financeService.GetTodaysChallengesAsync();
            var completedToday = await _financeService.GetChallengesCompletedTodayAsync();
            var completedWeek = await _financeService.GetChallengesCompletedThisWeekAsync();

            _content.Children.Clear();

            _content.Children.Add(new Label { Text = "Daily Challenges", FontFamily = "SpaceGroteskBold", FontSize = 24, TextColor = AppColors.Dark });
            _content.Children.Add(new Label { Text = "Complete challenges to earn XP and build healthy habits", FontSize = 13, TextColor = AppColors.Muted, FontFamily = "SpaceGroteskRegular" });

            // Level bar
            var xpProgress = levelInfo.XpForNextLevel > 0 ? (double)levelInfo.CurrentXp / levelInfo.XpForNextLevel : 0;
            var levelBar = new Border
            {
                BackgroundColor = AppColors.Surface,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Stroke = new SolidColorBrush(AppColors.Border),
                StrokeThickness = 1,
                Padding = new Thickness(14),
                Content = new HorizontalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Border
                        {
                            BackgroundColor = Color.FromArgb("#6577F3"),
                            StrokeShape = new RoundRectangle { CornerRadius = 10 },
                            Stroke = Colors.Transparent,
                            WidthRequest = 44,
                            HeightRequest = 44,
                            Padding = 0,
                            Content = new Label
                            {
                                Text = levelInfo.Level.ToString(),
                                FontSize = 18,
                                FontFamily = "SpaceGroteskBold",
                                TextColor = Colors.White,
                                HorizontalTextAlignment = TextAlignment.Center,
                                VerticalTextAlignment = TextAlignment.Center
                            }
                        },
                        new VerticalStackLayout
                        {
                            Spacing = 4,
                            VerticalOptions = LayoutOptions.Center,
                            HorizontalOptions = LayoutOptions.Fill,
                            Children =
                            {
                                new Grid
                                {
                                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                                    Children =
                                    {
                                        new Label { Text = levelInfo.LevelName, FontFamily = "SpaceGroteskBold", FontSize = 14, TextColor = AppColors.Dark },
                                        new Label { Text = $"{levelInfo.CurrentXp} / {levelInfo.XpForNextLevel} XP", FontSize = 12, TextColor = AppColors.Muted, HorizontalOptions = LayoutOptions.End, FontFamily = "SpaceGroteskRegular" }
                                    }
                                },
                                new ProgressBar { Progress = xpProgress, ProgressColor = Color.FromArgb("#6577F3"), HeightRequest = 6 }
                            }
                        }
                    }
                }
            };
            _content.Children.Add(levelBar);

            // Stats row
            var statsRow = new HorizontalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    CreateStatChip($"{AppIcons.GlyphCheck} {completedToday} today", Color.FromArgb("#ECFDF5"), AppColors.Success),
                    CreateStatChip($"{AppIcons.GlyphCalendarWeek} {completedWeek} this week", Color.FromArgb("#F0F1FE"), Color.FromArgb("#6577F3"))
                }
            };
            _content.Children.Add(statsRow);

            _content.Children.Add(new Label { Text = "TODAY'S CHALLENGES", FontSize = 11, FontFamily = "SpaceGroteskBold", TextColor = AppColors.Muted, Margin = new Thickness(0, 8, 0, 0) });

            if (challenges.Count == 0)
            {
                _content.Children.Add(new Border
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
                            new Label { Text = AppIcons.GlyphBolt, FontFamily = "FontAwesomeSolid", FontSize = 32, HorizontalOptions = LayoutOptions.Center, TextColor = AppColors.Muted },
                            new Label { Text = "No Challenges Yet", FontFamily = "SpaceGroteskBold", FontSize = 16, TextColor = AppColors.Dark, HorizontalTextAlignment = TextAlignment.Center },
                            new Label { Text = "Challenges are generated daily. Check back soon!", FontSize = 13, TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, FontFamily = "SpaceGroteskRegular" }
                        }
                    }
                });
                return;
            }

            foreach (var challenge in challenges)
            {
                var isCompleted = challenge.Status == ChallengeStatus.Completed;
                var isExpired = challenge.Status == ChallengeStatus.Expired;

                var iconBg = isCompleted ? Color.FromArgb("#ECFDF5") : Color.FromArgb("#F0F1FE");
                var iconColor = isCompleted ? AppColors.Success : Color.FromArgb("#6577F3");
                var iconText = isCompleted ? AppIcons.GlyphCheck : AppIcons.GlyphBolt;

                var iconFrame = new Border
                {
                    BackgroundColor = iconBg,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Stroke = Colors.Transparent,
                    WidthRequest = 44,
                    HeightRequest = 44,
                    Padding = 0,
                    Content = new Label
                    {
                        Text = iconText,
                        FontFamily = "FontAwesomeSolid",
                        FontSize = 18,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center,
                        TextColor = iconColor
                    }
                };

                var freqLabel = challenge.Frequency == ChallengeFrequency.Daily ? "Daily" : "Weekly";
                var freqBadge = new Border
                {
                    BackgroundColor = Color.FromArgb("#EDE9FE"),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Stroke = Colors.Transparent,
                    Padding = new Thickness(6, 2),
                    Content = new Label { Text = freqLabel, FontSize = 9, FontFamily = "SpaceGroteskBold", TextColor = Color.FromArgb("#7C3AED") }
                };

                var titleRow = new HorizontalStackLayout
                {
                    Spacing = 6,
                    Children = { new Label { Text = challenge.Title, FontFamily = "SpaceGroteskBold", FontSize = 14, TextColor = AppColors.Dark }, freqBadge }
                };

                var infoStack = new VerticalStackLayout
                {
                    Spacing = 2,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children = { titleRow, new Label { Text = challenge.Description, FontSize = 11, TextColor = AppColors.Muted, FontFamily = "SpaceGroteskRegular" } }
                };

                var xpLabel = new Label
                {
                    Text = $"+{challenge.XpReward} XP",
                    FontSize = 13,
                    FontFamily = "SpaceGroteskBold",
                    TextColor = isCompleted ? AppColors.Success : Color.FromArgb("#6577F3"),
                    VerticalOptions = LayoutOptions.Center
                };

                var grid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(52)),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 12
                };
                Grid.SetColumn(iconFrame, 0);
                Grid.SetColumn(infoStack, 1);
                Grid.SetColumn(xpLabel, 2);
                grid.Children.Add(iconFrame);
                grid.Children.Add(infoStack);
                grid.Children.Add(xpLabel);

                var card = new Border
                {
                    BackgroundColor = isCompleted ? Color.FromArgb("#F0FDF9") : AppColors.Surface,
                    StrokeShape = new RoundRectangle { CornerRadius = 16 },
                    Stroke = new SolidColorBrush(isCompleted ? Color.FromArgb("#A7F3D0") : AppColors.Border),
                    StrokeThickness = 1,
                    Padding = new Thickness(14),
                    Opacity = isExpired ? 0.5 : 1.0,
                    Content = grid
                };

                if (!isCompleted && !isExpired)
                {
                    var tapGesture = new TapGestureRecognizer();
                    var challengeId = challenge.Id;
                    tapGesture.Tapped += async (_, _) =>
                    {
                        var confirm = await DisplayAlertAsync("Complete Challenge", $"Mark '{challenge.Title}' as completed?", "Complete", "Cancel");
                        if (confirm)
                        {
                            await _financeService.CompleteChallengeAsync(challengeId);
                            await LoadAsync();
                        }
                    };
                    card.GestureRecognizers.Add(tapGesture);
                }

                _content.Children.Add(card);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private static Border CreateStatChip(string text, Color bgColor, Color textColor)
    {
        return new Border
        {
            BackgroundColor = bgColor,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Stroke = Colors.Transparent,
            Padding = new Thickness(12, 6),
            Content = new Label
            {
                Text = text,
                FontFamily = "FontAwesomeSolid",
                FontSize = 12,
                TextColor = textColor
            }
        };
    }
}
