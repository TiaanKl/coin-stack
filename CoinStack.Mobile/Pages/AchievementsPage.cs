namespace CoinStack.Mobile.Pages;

/// <summary>
/// Achievements &amp; Level page – Duolingo-style progression UI (display-only).
/// </summary>
public sealed class AchievementsPage : ContentPage
{
    public AchievementsPage()
    {
        Title = "Achievements";

        // Level progress card
        var levelCard = new Frame
        {
            CornerRadius = 16,
            Padding = new Thickness(16),
            HasShadow = false,
            BorderColor = Color.FromArgb("#E4E7EC"),
            BackgroundColor = Colors.White,
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
                            new Frame
                            {
                                CornerRadius = 12,
                                WidthRequest = 48,
                                HeightRequest = 48,
                                Padding = 0,
                                HasShadow = false,
                                BackgroundColor = Color.FromArgb("#6577F3"),
                                Content = new Label
                                {
                                    Text = "1",
                                    FontSize = 20,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Colors.White,
                                    HorizontalTextAlignment = TextAlignment.Center,
                                    VerticalTextAlignment = TextAlignment.Center,
                                },
                            },
                            new VerticalStackLayout
                            {
                                Spacing = 2,
                                VerticalOptions = LayoutOptions.Center,
                                Children =
                                {
                                    new Label { Text = "Penny Starter", FontSize = 16, FontAttributes = FontAttributes.Bold },
                                    new Label { Text = "0 / 50 XP to Level 2", FontSize = 12, TextColor = Colors.Gray },
                                },
                            },
                        },
                    },
                    new ProgressBar { Progress = 0, ProgressColor = Color.FromArgb("#6577F3") },
                },
            },
        };

        // Achievement categories
        var content = new VerticalStackLayout
        {
            Padding = new Thickness(16),
            Spacing = 16,
            Children =
            {
                new Label { Text = "Level & Progress", FontSize = 22, FontAttributes = FontAttributes.Bold },
                levelCard,
            },
        };

        // Sample achievements by category
        (string Category, string Icon, (string Title, string Desc, int Xp, bool Unlocked)[] Items)[] categories =
        [
            ("🔥 STREAKS", "fire", [
                ("First Spark", "Log in 2 days in a row", 10, false),
                ("Consistency King", "Maintain a 7-day streak", 25, false),
                ("Unstoppable", "Maintain a 30-day streak", 100, false),
            ]),
            ("📊 BUDGETING", "chart-pie", [
                ("Budget Builder", "Create your first bucket", 10, false),
                ("Under Budget", "Stay under budget for a full month", 50, false),
                ("Penny Pincher", "Come within 5% of a bucket without going over", 25, false),
            ]),
            ("🧠 MINDFULNESS", "brain", [
                ("Self-Aware", "Complete your first reflection", 10, false),
                ("Thought Catcher", "Log your first CBT thought record", 15, false),
                ("Pattern Spotter", "Identify the same distortion 3 times", 25, false),
            ]),
            ("💰 SAVING", "piggy-bank", [
                ("First Stash", "Save your first amount", 10, false),
                ("Goal Getter", "Complete a savings goal", 50, false),
            ]),
            ("⚡ CHALLENGES", "bolt", [
                ("Challenge Accepted", "Complete your first challenge", 10, false),
                ("On a Roll", "Complete 5 challenges in a week", 25, false),
            ]),
        ];

        foreach (var (category, _, items) in categories)
        {
            content.Children.Add(new Label
            {
                Text = category,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Gray,
                Margin = new Thickness(0, 8, 0, 0),
            });

            foreach (var (title, desc, xp, unlocked) in items)
            {
                var card = new Frame
                {
                    CornerRadius = 14,
                    Padding = new Thickness(14),
                    HasShadow = false,
                    Opacity = unlocked ? 1 : 0.5,
                    BorderColor = unlocked ? Color.FromArgb("#6577F3") : Color.FromArgb("#E4E7EC"),
                    BackgroundColor = unlocked ? Color.FromArgb("#F0F1FE") : Colors.White,
                    Content = new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            new Frame
                            {
                                CornerRadius = 10,
                                WidthRequest = 40,
                                HeightRequest = 40,
                                Padding = 0,
                                HasShadow = false,
                                BackgroundColor = unlocked ? Color.FromArgb("#DDE0FA") : Color.FromArgb("#F3F4F6"),
                                Content = new Label
                                {
                                    Text = unlocked ? "✓" : "🏅",
                                    FontSize = 18,
                                    HorizontalTextAlignment = TextAlignment.Center,
                                    VerticalTextAlignment = TextAlignment.Center,
                                },
                            },
                            new VerticalStackLayout
                            {
                                Spacing = 2,
                                VerticalOptions = LayoutOptions.Center,
                                Children =
                                {
                                    new Label { Text = title, FontSize = 14, FontAttributes = FontAttributes.Bold },
                                    new Label { Text = desc, FontSize = 11, TextColor = Colors.Gray },
                                },
                            },
                            new Label
                            {
                                Text = $"+{xp}XP",
                                FontSize = 12,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = unlocked ? Color.FromArgb("#6577F3") : Colors.Gray,
                                VerticalOptions = LayoutOptions.Center,
                                HorizontalOptions = LayoutOptions.EndAndExpand,
                            },
                        },
                    },
                };
                content.Children.Add(card);
            }
        }

        Content = new ScrollView { Content = content };
    }
}
