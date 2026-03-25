namespace CoinStack.Mobile.Pages;

/// <summary>
/// Mindset Journal page – CBT thought records UI (display-only, no live service wiring).
/// </summary>
public sealed class CbtJournalPage : ContentPage
{
    private readonly VerticalStackLayout _entryList;

    public CbtJournalPage()
    {
        Title = "Mindset Journal";

        var headerLabel = new Label
        {
            Text = "Money Mindset Journal",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
        };

        var subtitleLabel = new Label
        {
            Text = "Challenge unhelpful financial thoughts with CBT techniques",
            FontSize = 13,
            TextColor = Colors.Gray,
        };

        var addButton = new Button
        {
            Text = "＋  New Thought Record",
            BackgroundColor = Color.FromArgb("#6577F3"),
            TextColor = Colors.White,
            CornerRadius = 24,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            HeightRequest = 48,
            Margin = new Thickness(0, 8, 0, 0),
        };
        addButton.Clicked += OnNewEntryClicked;

        // Exercises quick access
        var exercisesHeader = new Label
        {
            Text = "GUIDED EXERCISES",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Gray,
            Margin = new Thickness(0, 16, 0, 4),
        };

        var exercisesLayout = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
        };

        string[] exercises = ["Thought Record", "Mindful Spending Pause", "Values Check", "Gratitude Audit", "Letter to Future You", "Trigger Map"];
        foreach (var name in exercises)
        {
            var chip = new Frame
            {
                CornerRadius = 16,
                Padding = new Thickness(12, 8),
                Margin = new Thickness(0, 0, 8, 8),
                HasShadow = false,
                BorderColor = Color.FromArgb("#E4E7EC"),
                BackgroundColor = Color.FromArgb("#F8F9FA"),
                Content = new Label
                {
                    Text = name,
                    FontSize = 12,
                    TextColor = Color.FromArgb("#6577F3"),
                    FontAttributes = FontAttributes.Bold,
                },
            };
            exercisesLayout.Children.Add(chip);
        }

        // Empty state
        _entryList = new VerticalStackLayout { Spacing = 10 };

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
                    new Label { Text = "🧠", FontSize = 32, HorizontalOptions = LayoutOptions.Center },
                    new Label { Text = "Start Your Mindset Journal", FontSize = 16, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = "Record financial thoughts and challenge them with evidence-based CBT techniques.", FontSize = 13, TextColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center },
                },
            },
        };
        _entryList.Children.Add(emptyState);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 6,
                Children =
                {
                    headerLabel,
                    subtitleLabel,
                    addButton,
                    exercisesHeader,
                    exercisesLayout,
                    new Label { Text = "JOURNAL ENTRIES", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Colors.Gray, Margin = new Thickness(0, 12, 0, 4) },
                    _entryList,
                },
            },
        };
    }

    private async void OnNewEntryClicked(object? sender, EventArgs e)
    {
        await DisplayAlert("Coming Soon", "CBT thought record will be available when the mobile service is wired up.", "OK");
    }
}
