using CoinStack.Data.Entities;
using CoinStack.Mobile.Services;

namespace CoinStack.Mobile.Pages;

public sealed class ReflectionsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Label _pendingPromptLabel;
    private readonly Picker _emotionPicker;
    private readonly Slider _moodBeforeSlider;
    private readonly Slider _moodAfterSlider;
    private readonly Editor _responseEditor;
    private readonly VerticalStackLayout _recentList;

    private int? _pendingReflectionId;

    public ReflectionsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Reflections";

        _pendingPromptLabel = new Label { FontSize = 14 };
        _emotionPicker = new Picker { Title = "Emotion", ItemsSource = Enum.GetValues<EmotionTag>().ToList() };
        _moodBeforeSlider = new Slider { Minimum = 1, Maximum = 10, Value = 5 };
        _moodAfterSlider = new Slider { Minimum = 1, Maximum = 10, Value = 5 };
        _responseEditor = new Editor { Placeholder = "Write your reflection...", AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 90 };

        var createButton = new Button { Text = "Create Manual Reflection" };
        createButton.Clicked += async (_, _) => await CreateManualAsync();

        var completeButton = new Button { Text = "Complete Reflection (+3 pts)" };
        completeButton.Clicked += async (_, _) => await CompleteAsync();

        _recentList = new VerticalStackLayout { Spacing = 8 };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    createButton,
                    new Label { Text = "Pending Reflection", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _pendingPromptLabel,
                    new Label { Text = "Emotion" },
                    _emotionPicker,
                    new Label { Text = "Mood Before" },
                    _moodBeforeSlider,
                    new Label { Text = "Mood After" },
                    _moodAfterSlider,
                    _responseEditor,
                    completeButton,
                    new BoxView { HeightRequest = 1 },
                    new Label { Text = "Recent Reflections", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _recentList
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task CreateManualAsync()
    {
        await _financeService.CreateManualReflectionAsync();
        await LoadAsync();
    }

    private async Task CompleteAsync()
    {
        if (_pendingReflectionId is null)
        {
            await DisplayAlertAsync("No pending reflection", "Create or trigger a reflection first.", "OK");
            return;
        }

        var response = _responseEditor.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(response))
        {
            await DisplayAlertAsync("Validation", "Please write a reflection response.", "OK");
            return;
        }

        var emotion = _emotionPicker.SelectedItem as EmotionTag?;

        try
        {
            await _financeService.CompleteReflectionAsync(
                _pendingReflectionId.Value,
                response,
                (int)Math.Round(_moodBeforeSlider.Value),
                (int)Math.Round(_moodAfterSlider.Value),
                emotion);

            _responseEditor.Text = string.Empty;
            _moodBeforeSlider.Value = 5;
            _moodAfterSlider.Value = 5;
            _emotionPicker.SelectedItem = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            var pending = await _financeService.GetPendingReflectionAsync();
            _pendingReflectionId = pending?.Id;
            _pendingPromptLabel.Text = pending is null ? "No pending reflection." : pending.Prompt;

            var recent = await _financeService.GetRecentReflectionsAsync();
            _recentList.Children.Clear();

            if (recent.Count == 0)
            {
                _recentList.Children.Add(new Label { Text = "No reflections yet." });
                return;
            }

            foreach (var reflection in recent)
            {
                _recentList.Children.Add(new Label
                {
                    Text = $"{reflection.CreatedAtUtc:dd MMM} • {reflection.Trigger} • {(reflection.IsCompleted ? "Completed" : "Pending")}",
                    FontSize = 13,
                    FontAttributes = reflection.IsCompleted ? FontAttributes.None : FontAttributes.Bold
                });

                if (reflection.IsCompleted && !string.IsNullOrWhiteSpace(reflection.Response))
                {
                    _recentList.Children.Add(new Label
                    {
                        Text = reflection.Response,
                        FontSize = 12
                    });
                }

                _recentList.Children.Add(new BoxView { HeightRequest = 1 });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
