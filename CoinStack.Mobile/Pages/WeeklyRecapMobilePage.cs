namespace CoinStack.Mobile.Pages;

/// <summary>
/// Weekly Recap page – summary of financial activity (display-only).
/// </summary>
public sealed class WeeklyRecapMobilePage : ContentPage
{
    public WeeklyRecapMobilePage()
    {
        Title = "Weekly Recap";

        var emptyState = new Frame
        {
            CornerRadius = 16,
            Padding = new Thickness(24),
            HasShadow = false,
            BorderColor = Color.FromArgb("#E4E7EC"),
            BackgroundColor = Colors.White,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = "📅", FontSize = 32, HorizontalOptions = LayoutOptions.Center },
                    new Label { Text = "No Recaps Yet", FontSize = 16, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = "Weekly recaps will appear here at the end of each week.", FontSize = 13, TextColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center },
                },
            },
        };

        // Sample recap card
        var sampleRecap = BuildRecapCard(
            weekLabel: "Week 1, 2025",
            income: 5000,
            spent: 3200,
            saved: 1800,
            points: 45,
            challenges: 8,
            reflections: 3,
            streak: 7,
            topCategory: "Groceries",
            insight: "You saved $1,800 this week — nice work! Most of your spending went to Groceries. Great hustle — you completed 8 challenges! Your 7-day streak is on fire!"
        );

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Weekly Recap", FontSize = 22, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Your financial week at a glance", FontSize = 13, TextColor = Colors.Gray },
                    sampleRecap,
                    emptyState,
                },
            },
        };
    }

    private static Frame BuildRecapCard(
        string weekLabel,
        decimal income,
        decimal spent,
        decimal saved,
        int points,
        int challenges,
        int reflections,
        int streak,
        string topCategory,
        string insight)
    {
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
            ColumnSpacing = 8,
        };

        statsGrid.Add(CreateStatCell("Income", income.ToString("C0"), Color.FromArgb("#059669")), 0, 0);
        statsGrid.Add(CreateStatCell("Spent", spent.ToString("C0"), Color.FromArgb("#EF4444")), 1, 0);
        statsGrid.Add(CreateStatCell("Saved", saved.ToString("C0"), saved >= 0 ? Color.FromArgb("#059669") : Color.FromArgb("#EF4444")), 2, 0);
        statsGrid.Add(CreateStatCell("Points", points.ToString(), Color.FromArgb("#6577F3")), 3, 0);

        var badges = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
        };

        badges.Children.Add(CreateBadge($"⚡ {challenges} challenges", Color.FromArgb("#F0F1FE"), Color.FromArgb("#6577F3")));
        badges.Children.Add(CreateBadge($"💡 {reflections} reflections", Color.FromArgb("#F5F3FF"), Color.FromArgb("#7C3AED")));
        badges.Children.Add(CreateBadge($"🔥 {streak} day streak", Color.FromArgb("#FFFBEB"), Color.FromArgb("#D97706")));
        badges.Children.Add(CreateBadge($"🏷 Top: {topCategory}", Color.FromArgb("#F3F4F6"), Color.FromArgb("#4B5563")));

        var insightFrame = new Frame
        {
            CornerRadius = 12,
            Padding = new Thickness(12),
            HasShadow = false,
            BackgroundColor = Color.FromArgb("#F0F1FE"),
            Content = new Label
            {
                Text = $"✨ {insight}",
                FontSize = 13,
                TextColor = Color.FromArgb("#4338CA"),
            },
        };

        return new Frame
        {
            CornerRadius = 16,
            Padding = new Thickness(16),
            HasShadow = false,
            BorderColor = Color.FromArgb("#C7D2FE"),
            BackgroundColor = Colors.White,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Label { Text = "📅", FontSize = 18 },
                            new Label { Text = weekLabel, FontSize = 16, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center },
                        },
                    },
                    statsGrid,
                    badges,
                    insightFrame,
                },
            },
        };
    }

    private static Frame CreateStatCell(string label, string value, Color valueColor)
    {
        return new Frame
        {
            CornerRadius = 10,
            Padding = new Thickness(8),
            HasShadow = false,
            BackgroundColor = Color.FromArgb("#F8F9FA"),
            Content = new VerticalStackLayout
            {
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = label, FontSize = 10, TextColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = value, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = valueColor, HorizontalTextAlignment = TextAlignment.Center },
                },
            },
        };
    }

    private static Frame CreateBadge(string text, Color bgColor, Color textColor)
    {
        return new Frame
        {
            CornerRadius = 12,
            Padding = new Thickness(10, 4),
            Margin = new Thickness(0, 0, 6, 6),
            HasShadow = false,
            BackgroundColor = bgColor,
            Content = new Label
            {
                Text = text,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = textColor,
            },
        };
    }
}
