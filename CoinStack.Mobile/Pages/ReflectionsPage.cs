using CoinStack.Data.Entities;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class ReflectionsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Label _pendingPromptLabel;
    private readonly Picker _emotionPicker;
    private readonly Slider _moodBeforeSlider;
    private readonly Label _moodBeforeValueLabel;
    private readonly Slider _moodAfterSlider;
    private readonly Label _moodAfterValueLabel;
    private readonly Editor _responseEditor;
    private readonly VerticalStackLayout _recentList;

    private int? _pendingReflectionId;

    public ReflectionsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Reflections";

        _pendingPromptLabel = new Label { FontSize = 14, FontFamily = "InterRegular", TextColor = AppColors.Muted };
        _emotionPicker = new Picker { Title = "Select emotion", ItemsSource = Enum.GetValues<EmotionTag>().ToList() };
        _moodBeforeSlider = new Slider { Minimum = 1, Maximum = 10, Value = 5 };
        _moodBeforeValueLabel = new Label { Text = "5", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark, HorizontalTextAlignment = TextAlignment.Center };
        _moodAfterSlider = new Slider { Minimum = 1, Maximum = 10, Value = 5 };
        _moodAfterValueLabel = new Label { Text = "5", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark, HorizontalTextAlignment = TextAlignment.Center };
        _responseEditor = new Editor { Placeholder = "Write your reflection...", AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 100 };

        _moodBeforeSlider.ValueChanged += (_, e) => _moodBeforeValueLabel.Text = ((int)Math.Round(e.NewValue)).ToString();
        _moodAfterSlider.ValueChanged += (_, e) => _moodAfterValueLabel.Text = ((int)Math.Round(e.NewValue)).ToString();

        var createButton = new Button { Text = "Create Manual Reflection" };
        createButton.Clicked += async (_, _) => await CreateManualAsync();

        var completeButton = new Button { Text = "Complete Reflection (+3 pts)" };
        completeButton.Clicked += async (_, _) => await CompleteAsync();

        _recentList = new VerticalStackLayout { Spacing = 10 };

        // ── Pending Reflection Card ──
        var pendingCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Pending Reflection", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _pendingPromptLabel
            }
        });

        // ── Emotion Card ──
        var emotionCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Emotion", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Muted },
                _emotionPicker
            }
        });

        // ── Mood Card ──
        var moodCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = "Mood Before", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Muted },
                _moodBeforeValueLabel,
                _moodBeforeSlider,
                new Label { Text = "Mood After", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Muted },
                _moodAfterValueLabel,
                _moodAfterSlider
            }
        });

        // ── Response Card ──
        var responseCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Your Reflection", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Muted },
                _responseEditor
            }
        });

        // ── Recent Reflections Card ──
        var recentCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Recent Reflections", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _recentList
            }
        });

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children = { createButton, pendingCard, emotionCard, moodCard, responseCard, completeButton, recentCard }
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
                _recentList.Children.Add(new Label { Text = "No reflections yet.", FontFamily = "InterRegular", TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 16) });
                return;
            }

            foreach (var reflection in recent)
            {
                var statusColor = reflection.IsCompleted ? AppColors.Success : AppColors.Warning;
                var statusText = reflection.IsCompleted ? "Completed" : "Pending";

                var row = new VerticalStackLayout { Spacing = 4 };
                row.Children.Add(new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    Children =
                    {
                        new Label { Text = $"{reflection.CreatedAtUtc:dd MMM} · {reflection.Trigger}", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark },
                    }
                });

                var badge = new Border
                {
                    BackgroundColor = reflection.IsCompleted ? AppColors.BgSuccess : AppColors.BgWarning,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Stroke = Brush.Transparent,
                    Padding = new Thickness(8, 4),
                    Content = new Label { Text = statusText, FontSize = 11, FontFamily = "InterBold", TextColor = statusColor },
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Center
                };
                ((Grid)row.Children[0]).Add(badge, 1, 0);

                if (reflection.IsCompleted && !string.IsNullOrWhiteSpace(reflection.Response))
                {
                    row.Children.Add(new Label { Text = reflection.Response, FontSize = 12, FontFamily = "InterRegular", TextColor = AppColors.Muted, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 2 });
                }

                _recentList.Children.Add(new Border
                {
                    BackgroundColor = AppColors.SurfaceDim,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Stroke = Brush.Transparent,
                    Padding = new Thickness(12, 10),
                    Content = row
                });
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private static Border CreateCard(View content) => new()
    {
        BackgroundColor = AppColors.Surface,
        StrokeShape = new RoundRectangle { CornerRadius = 16 },
        Stroke = new SolidColorBrush(AppColors.Border),
        StrokeThickness = 1,
        Padding = new Thickness(16),
        Content = content
    };
}
