using CoinStack.Data.Entities;
using CoinStack.Mobile.Core;
using CoinStack.Mobile.Helpers;
using CoinStack.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CoinStack.Mobile.Pages;

public sealed class GoalsPage : ContentPage
{
    private readonly IMobileFinanceService _financeService;

    private readonly Entry _nameEntry;
    private readonly Entry _targetEntry;
    private readonly Entry _currentEntry;
    private readonly DatePicker _targetDatePicker;
    private readonly VerticalStackLayout _list;

    public GoalsPage(IMobileFinanceService financeService)
    {
        _financeService = financeService;
        Title = "Goals";

        _nameEntry = new Entry { Placeholder = "Goal name" };
        _targetEntry = new Entry { Placeholder = "Target amount", Keyboard = Keyboard.Numeric };
        _currentEntry = new Entry { Placeholder = "Current amount (optional)", Keyboard = Keyboard.Numeric };
        _targetDatePicker = new DatePicker { Date = DateTime.UtcNow.Date.AddMonths(3) };

        var addButton = new Button { Text = "Add Goal" };
        addButton.Clicked += async (_, _) => await AddGoalAsync();

        _list = new VerticalStackLayout { Spacing = 10 };

        // ── Add Goal Card ──
        var formCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = "New Goal", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _nameEntry,
                _targetEntry,
                _currentEntry,
                new Label { Text = "Target date", FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Muted },
                _targetDatePicker,
                addButton
            }
        });

        // ── Goals List Card ──
        var listCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Goals", FontFamily = "InterBold", FontSize = 16, TextColor = AppColors.Dark },
                _list
            }
        });

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 16,
                Children = { formCard, listCard }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task AddGoalAsync()
    {
        if (!decimal.TryParse(_targetEntry.Text, out var target))
        {
            await DisplayAlertAsync("Validation", "Enter a valid target amount.", "OK");
            return;
        }

        var current = 0m;
        if (!string.IsNullOrWhiteSpace(_currentEntry.Text) && !decimal.TryParse(_currentEntry.Text, out current))
        {
            await DisplayAlertAsync("Validation", "Current amount must be valid when provided.", "OK");
            return;
        }

        try
        {
            await _financeService.AddGoalAsync(_nameEntry.Text ?? string.Empty, target, current, _targetDatePicker.Date);
            _nameEntry.Text = string.Empty;
            _targetEntry.Text = string.Empty;
            _currentEntry.Text = string.Empty;
            _targetDatePicker.Date = DateTime.UtcNow.Date.AddMonths(3);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private async Task ContributeAsync(int goalId)
    {
        var input = await DisplayPromptAsync("Goal Contribution", "Amount to add:", "Save", "Cancel", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(input) || !decimal.TryParse(input, out var amount))
        {
            return;
        }

        await _financeService.AddGoalContributionAsync(goalId, amount);
        await LoadAsync();
    }

    private async Task DeleteAsync(int goalId)
    {
        await _financeService.DeleteGoalAsync(goalId);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var settings = await _financeService.GetSettingsAsync();
            var goals = await _financeService.GetGoalsAsync();

            _list.Children.Clear();
            if (goals.Count == 0)
            {
                _list.Children.Add(new Label { Text = "No goals yet.", FontFamily = "InterRegular", TextColor = AppColors.Muted, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 16) });
                return;
            }

            foreach (var goal in goals)
            {
                var percent = goal.TargetAmount > 0 ? Math.Min(100, (goal.CurrentAmount / goal.TargetAmount) * 100m) : 0m;
                var statusColor = goal.Status == GoalStatus.Completed ? AppColors.Success : AppColors.Dark;

                var item = new VerticalStackLayout { Spacing = 6 };

                var header = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                };
                header.Add(new Label { Text = goal.Name, FontFamily = "InterBold", FontSize = 14, TextColor = AppColors.Dark }, 0, 0);
                var statusBadge = new Border
                {
                    BackgroundColor = goal.Status == GoalStatus.Completed ? AppColors.BgSuccess : AppColors.SurfaceContainer,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Stroke = Brush.Transparent,
                    Padding = new Thickness(8, 4),
                    Content = new Label { Text = goal.Status.ToString(), FontSize = 11, FontFamily = "InterBold", TextColor = statusColor },
                    HorizontalOptions = LayoutOptions.End
                };
                header.Add(statusBadge, 1, 0);
                item.Children.Add(header);

                item.Children.Add(new ProgressBar { Progress = (double)(percent / 100m), ProgressColor = AppColors.Accent, HeightRequest = 6 });
                item.Children.Add(new Label
                {
                    Text = $"{MoneyDisplay.Format(settings.Currency, goal.CurrentAmount)} / {MoneyDisplay.Format(settings.Currency, goal.TargetAmount)} · {percent:0.#}%",
                    FontSize = 12,
                    FontFamily = "InterRegular",
                    TextColor = AppColors.Muted
                });

                if (goal.TargetDateUtc.HasValue)
                {
                    item.Children.Add(new Label
                    {
                        Text = $"Target: {goal.TargetDateUtc.Value:dd MMM yyyy}",
                        FontSize = 11,
                        FontFamily = "InterRegular",
                        TextColor = AppColors.Muted
                    });
                }

                var actions = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
                var contributeButton = new Button { Text = "Add", FontSize = 12, Padding = new Thickness(12, 6), HeightRequest = 36, CornerRadius = 18 };
                var deleteButton = new Button { Text = "Delete", FontSize = 12, Padding = new Thickness(12, 6), HeightRequest = 36, CornerRadius = 18, BackgroundColor = AppColors.Danger };
                var id = goal.Id;
                contributeButton.Clicked += async (_, _) => await ContributeAsync(id);
                deleteButton.Clicked += async (_, _) => await DeleteAsync(id);
                actions.Children.Add(contributeButton);
                actions.Children.Add(deleteButton);
                item.Children.Add(actions);

                _list.Children.Add(new Border
                {
                    BackgroundColor = AppColors.SurfaceDim,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Stroke = new SolidColorBrush(AppColors.Border),
                    StrokeThickness = 1,
                    Padding = new Thickness(14, 12),
                    Content = item
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
