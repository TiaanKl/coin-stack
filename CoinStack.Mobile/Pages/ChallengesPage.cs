namespace CoinStack.Mobile.Pages;

/// <summary>
/// Daily Challenges page – Duolingo-style daily tasks UI (display-only).
/// </summary>
public sealed class ChallengesPage : ContentPage
{
    public ChallengesPage()
    {
        Title = "Challenges";

        // Level bar
        var levelBar = new Frame
        {
            CornerRadius = 16,
            Padding = new Thickness(14),
            HasShadow = false,
            BorderColor = Color.FromArgb("#E4E7EC"),
            BackgroundColor = Colors.White,
            Content = new HorizontalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Frame
                    {
                        CornerRadius = 10,
                        WidthRequest = 44,
                        HeightRequest = 44,
                        Padding = 0,
                        HasShadow = false,
                        BackgroundColor = Color.FromArgb("#6577F3"),
                        Content = new Label
                        {
                            Text = "1",
                            FontSize = 18,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Colors.White,
                            HorizontalTextAlignment = TextAlignment.Center,
                            VerticalTextAlignment = TextAlignment.Center,
                        },
                    },
                    new VerticalStackLayout
                    {
                        Spacing = 4,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.FillAndExpand,
                        Children =
                        {
                            new HorizontalStackLayout
                            {
                                Children =
                                {
                                    new Label { Text = "Penny Starter", FontSize = 14, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.StartAndExpand },
                                    new Label { Text = "0 / 50 XP", FontSize = 12, TextColor = Colors.Gray, HorizontalOptions = LayoutOptions.End },
                                },
                            },
                            new ProgressBar { Progress = 0, ProgressColor = Color.FromArgb("#6577F3"), HeightRequest = 6 },
                        },
                    },
                },
            },
        };

        // Stats row
        var statsRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                CreateStatChip("✅ 0 today", Color.FromArgb("#ECFDF5"), Color.FromArgb("#059669")),
                CreateStatChip("📅 0 this week", Color.FromArgb("#F0F1FE"), Color.FromArgb("#6577F3")),
            },
        };

        // Sample challenges
        (string Title, string Desc, int Xp, string FreqLabel, bool IsCompleted)[] sampleChallenges =
        [
            ("No-Spend Day", "Don't log any expense transactions today.", 15, "Daily", false),
            ("Log Every Expense", "Record at least 3 expense transactions today.", 10, "Daily", false),
            ("Mindful Moment", "Complete a CBT thought record about a financial worry.", 15, "Daily", false),
        ];

        var challengeList = new VerticalStackLayout { Spacing = 10 };

        foreach (var (title, desc, xp, freqLabel, isCompleted) in sampleChallenges)
        {
            var iconFrame = new Frame
            {
                CornerRadius = 12,
                WidthRequest = 44,
                HeightRequest = 44,
                Padding = 0,
                HasShadow = false,
                BackgroundColor = isCompleted ? Color.FromArgb("#ECFDF5") : Color.FromArgb("#F0F1FE"),
                Content = new Label
                {
                    Text = isCompleted ? "✓" : "⚡",
                    FontSize = 18,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    TextColor = isCompleted ? Color.FromArgb("#059669") : Color.FromArgb("#6577F3"),
                },
            };

            var infoStack = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                Children =
                {
                    new HorizontalStackLayout
                    {
                        Spacing = 6,
                        Children =
                        {
                            new Label { Text = title, FontSize = 14, FontAttributes = FontAttributes.Bold },
                            new Frame
                            {
                                CornerRadius = 8,
                                Padding = new Thickness(6, 2),
                                HasShadow = false,
                                BackgroundColor = Color.FromArgb("#EDE9FE"),
                                Content = new Label { Text = freqLabel, FontSize = 9, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#7C3AED") },
                            },
                        },
                    },
                    new Label { Text = desc, FontSize = 11, TextColor = Colors.Gray },
                },
            };

            var xpLabel = new Label
            {
                Text = $"+{xp} XP",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = isCompleted ? Color.FromArgb("#059669") : Color.FromArgb("#6577F3"),
                VerticalOptions = LayoutOptions.Center,
            };

            var card = new Frame
            {
                CornerRadius = 16,
                Padding = new Thickness(14),
                HasShadow = false,
                BorderColor = isCompleted ? Color.FromArgb("#A7F3D0") : Color.FromArgb("#E4E7EC"),
                BackgroundColor = isCompleted ? Color.FromArgb("#F0FDF9") : Colors.White,
                Content = new HorizontalStackLayout
                {
                    Spacing = 12,
                    Children = { iconFrame, infoStack, xpLabel },
                },
            };

            challengeList.Children.Add(card);
        }

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Daily Challenges", FontSize = 22, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Complete challenges to earn XP and build healthy habits", FontSize = 13, TextColor = Colors.Gray },
                    levelBar,
                    statsRow,
                    new Label { Text = "TODAY'S CHALLENGES", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Colors.Gray, Margin = new Thickness(0, 8, 0, 0) },
                    challengeList,
                },
            },
        };
    }

    private static Frame CreateStatChip(string text, Color bgColor, Color textColor)
    {
        return new Frame
        {
            CornerRadius = 14,
            Padding = new Thickness(12, 6),
            HasShadow = false,
            BackgroundColor = bgColor,
            Content = new Label
            {
                Text = text,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = textColor,
            },
        };
    }
}
