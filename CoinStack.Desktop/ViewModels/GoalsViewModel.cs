using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoinStack.Desktop.ViewModels;

public partial class GoalsViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IGoalService _goals;
    private readonly ISettingsService _settings;
    private readonly IMoneyFormatter _money;

    [ObservableProperty] private string _newGoalName = "";
    [ObservableProperty] private decimal _newTargetAmount;
    [ObservableProperty] private string _activeCountText = "0";
    [ObservableProperty] private string _completedCountText = "0";

    public ObservableCollection<GoalRow> Goals { get; } = [];

    public GoalsViewModel(
        IGoalService goals,
        ISettingsService settings,
        IMoneyFormatter money)
    {
        _goals = goals;
        _settings = settings;
        _money = money;
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var settings = await _settings.GetAsync();
            var currency = settings.Currency;
            var goals = await _goals.GetAllAsync();

            Goals.Clear();
            foreach (var goal in goals.OrderBy(g => g.Status).ThenBy(g => g.Name))
            {
                var target = goal.TargetAmount <= 0 ? 1m : goal.TargetAmount;
                var progress = Math.Min((double)goal.CurrentAmount, (double)target);

                Goals.Add(new GoalRow
                {
                    Id = goal.Id,
                    Name = goal.Name,
                    CurrentText = _money.Format(currency, goal.CurrentAmount),
                    TargetText = _money.Format(currency, goal.TargetAmount),
                    TargetDateText = goal.TargetDateUtc?.ToLocalTime().ToString("MMM yyyy"),
                    StatusText = goal.Status.ToString(),
                    ProgressValue = progress,
                    ProgressMaximum = (double)target,
                    IsCompleted = goal.Status == GoalStatus.Completed || goal.CurrentAmount >= goal.TargetAmount
                });
            }

            ActiveCountText = goals.Count(g => g.Status == GoalStatus.Active).ToString();
            CompletedCountText = goals.Count(g => g.Status == GoalStatus.Completed).ToString();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load goals: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddGoalAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGoalName) || NewTargetAmount <= 0)
        {
            StatusMessage = "Enter a goal name and target amount.";
            return;
        }

        IsBusy = true;
        try
        {
            await _goals.CreateAsync(new Goal
            {
                Name = NewGoalName.Trim(),
                TargetAmount = NewTargetAmount,
                CurrentAmount = 0,
                Status = GoalStatus.Active
            });

            NewGoalName = "";
            NewTargetAmount = 0;
            StatusMessage = "Goal created.";
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not create goal: {ex.Message}";
            IsBusy = false;
        }
    }
}
