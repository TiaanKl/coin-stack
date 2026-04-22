using CoinStack.Data.Entities;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class CbtJournalPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;
    private readonly VerticalStackLayout _entryList;

    public CbtJournalPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Mindset Journal";

        _entryList = new VerticalStackLayout { Spacing = 10 };

        var addButton = new Button
        {
            Text = "New Thought Record",
            BackgroundColor = AppColors.Dark,
            TextColor = AppColors.Surface,
            CornerRadius = 24,
            FontFamily = "InterBold",
            FontSize = 14,
            HeightRequest = 48,
            Margin = new Thickness(0, 8, 0, 0)
        };
        addButton.Clicked += OnNewEntryClicked;

        // Exercises quick access
        var exercisesLayout = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start
        };

        string[] exercises = ["Thought Record", "Mindful Spending Pause", "Values Check", "Gratitude Audit", "Letter to Future You", "Trigger Map"];
        foreach (var name in exercises)
        {
            var chip = new Border
            {
                BackgroundColor = AppColors.SurfaceDim,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Stroke = new SolidColorBrush(AppColors.Border),
                StrokeThickness = 1,
                Padding = new Thickness(12, 8),
                Margin = new Thickness(0, 0, 8, 8),
                Content = new Label
                {
                    Text = name,
                    FontSize = 12,
                    TextColor = AppColors.AccentIndigo,
                    FontFamily = "InterBold"
                }
            };
            exercisesLayout.Children.Add(chip);
        }

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 6,
                Children =
                {
                    new Label { Text = "Money Mindset Journal", FontFamily = "InterBold", FontSize = 24, TextColor = AppColors.Dark },
                    new Label { Text = "Challenge unhelpful financial thoughts with CBT techniques", FontSize = 13, TextColor = AppColors.Muted, FontFamily = "InterRegular" },
                    addButton,
                    new Label { Text = "GUIDED EXERCISES", FontSize = 11, FontFamily = "InterBold", TextColor = AppColors.Muted, Margin = new Thickness(0, 16, 0, 4) },
                    exercisesLayout,
                    new Label { Text = "JOURNAL ENTRIES", FontSize = 11, FontFamily = "InterBold", TextColor = AppColors.Muted, Margin = new Thickness(0, 12, 0, 4) },
                    _entryList
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
            var entries = await _financeService.GetCbtEntriesAsync();
            _entryList.Children.Clear();

            if (entries.Count == 0)
            {
                _entryList.Children.Add(new Border
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
                            new Label { Text = AppIcons.GlyphBrain, FontFamily = "FontAwesomeSolid", FontSize = 32, HorizontalOptions = LayoutOptions.Center, TextColor = AppColors.Muted },
                            new Label { Text = "Start Your Mindset Journal", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark, HorizontalTextAlignment = TextAlignment.Center },
                            new Label { Text = "Record financial thoughts and challenge them with evidence-based CBT techniques.", FontSize = 13, TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, FontFamily = "InterRegular" }
                        }
                    }
                });
                return;
            }

            foreach (var entry in entries)
            {
                var moodDelta = entry.MoodAfter - entry.MoodBefore;
                var moodColor = moodDelta > 0 ? AppColors.Success : moodDelta < 0 ? AppColors.Danger : AppColors.Muted;
                var moodText = moodDelta > 0 ? $"+{moodDelta}" : moodDelta.ToString();

                var distortionText = entry.Distortion.HasValue ? entry.Distortion.Value.ToString() : "None";

                var card = new Border
                {
                    BackgroundColor = AppColors.Surface,
                    StrokeShape = new RoundRectangle { CornerRadius = 14 },
                    Stroke = new SolidColorBrush(AppColors.Border),
                    StrokeThickness = 1,
                    Padding = new Thickness(16),
                    Content = new VerticalStackLayout
                    {
                        Spacing = 6,
                        Children =
                        {
                            new Grid
                            {
                                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                                Children =
                                {
                                    new Label { Text = entry.Situation, FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark, LineBreakMode = LineBreakMode.TailTruncation },
                                    new Label { Text = entry.CreatedAtUtc.ToString("dd MMM"), FontSize = 11, TextColor = AppColors.Muted, FontFamily = "InterRegular", HorizontalOptions = LayoutOptions.End }
                                }
                            },
                            new Label { Text = $"Thought: {entry.AutomaticThought}", FontSize = 12, TextColor = AppColors.Muted, FontFamily = "InterRegular", LineBreakMode = LineBreakMode.TailTruncation },
                            new Label { Text = $"Response: {entry.RationalResponse}", FontSize = 12, TextColor = AppColors.Dark, FontFamily = "InterRegular", LineBreakMode = LineBreakMode.TailTruncation },
                            new HorizontalStackLayout
                            {
                                Spacing = 12,
                                Children =
                                {
                                    new Label { Text = $"Emotion: {entry.Emotion} ({entry.EmotionIntensity}/10)", FontSize = 11, TextColor = AppColors.Muted, FontFamily = "InterRegular" },
                                    new Label { Text = $"Mood: {moodText}", FontSize = 11, TextColor = moodColor, FontFamily = "InterBold" },
                                    new Label { Text = distortionText, FontSize = 11, TextColor = AppColors.AccentPurple, FontFamily = "InterRegular" }
                                }
                            }
                        }
                    }
                };

                _entryList.Children.Add(card);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async void OnNewEntryClicked(object? sender, EventArgs e)
    {
        var situation = await DisplayPromptAsync("Situation", "What happened? What triggered this thought?", "Next", "Cancel");
        if (string.IsNullOrWhiteSpace(situation)) return;

        var thought = await DisplayPromptAsync("Automatic Thought", "What went through your mind?", "Next", "Cancel");
        if (string.IsNullOrWhiteSpace(thought)) return;

        var emotion = await DisplayPromptAsync("Emotion", "What did you feel? (e.g., anxious, guilty, stressed)", "Next", "Cancel");
        if (string.IsNullOrWhiteSpace(emotion)) return;

        var response = await DisplayPromptAsync("Rational Response", "What's a more balanced way to think about this?", "Save", "Cancel");
        if (string.IsNullOrWhiteSpace(response)) return;

        try
        {
            await _financeService.AddCbtEntryAsync(situation, thought, emotion, 5, response, 4, 6);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
