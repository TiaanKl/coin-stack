using CoinStack.Data.Entities;
using CoinStack.Mobile.Core;
using CoinStack.Mobile.Services;

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

        _list = new VerticalStackLayout { Spacing = 8 };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 10,
                Children =
                {
                    _nameEntry,
                    _targetEntry,
                    _currentEntry,
                    new Label { Text = "Target date" },
                    _targetDatePicker,
                    addButton,
                    new BoxView { HeightRequest = 1 },
                    new Label { Text = "Goals", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    _list
                }
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
                _list.Children.Add(new Label { Text = "No goals yet." });
                return;
            }

            foreach (var goal in goals)
            {
                var percent = goal.TargetAmount > 0 ? Math.Min(100, (goal.CurrentAmount / goal.TargetAmount) * 100m) : 0m;

                var item = new VerticalStackLayout { Spacing = 4 };
                item.Children.Add(new Label
                {
                    Text = $"{goal.Name} ({goal.Status})",
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 14
                });
                item.Children.Add(new Label
                {
                    Text = $"{MoneyDisplay.Format(settings.Currency, goal.CurrentAmount)} / {MoneyDisplay.Format(settings.Currency, goal.TargetAmount)} • {percent:0.#}%",
                    FontSize = 13
                });
                if (goal.TargetDateUtc.HasValue)
                {
                    item.Children.Add(new Label
                    {
                        Text = $"Target date: {goal.TargetDateUtc.Value:dd MMM yyyy}",
                        FontSize = 12
                    });
                }

                var actions = new HorizontalStackLayout { Spacing = 8 };
                var contributeButton = new Button { Text = "Add", FontSize = 12, Padding = new Thickness(10, 4) };
                var deleteButton = new Button { Text = "Delete", FontSize = 12, Padding = new Thickness(10, 4) };
                var id = goal.Id;
                contributeButton.Clicked += async (_, _) => await ContributeAsync(id);
                deleteButton.Clicked += async (_, _) => await DeleteAsync(id);
                actions.Children.Add(contributeButton);
                actions.Children.Add(deleteButton);

                item.Children.Add(actions);
                item.Children.Add(new BoxView { HeightRequest = 1 });
                _list.Children.Add(item);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }
}
