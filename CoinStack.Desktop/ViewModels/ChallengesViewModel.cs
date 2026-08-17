// File: CoinStack.Desktop/ViewModels/ChallengesViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class ChallengesViewModel : ViewModelBase
{
    [ObservableProperty] private int _completedToday = 1;
    [ObservableProperty] private int _completedThisWeek = 4;
    [ObservableProperty] private int _currentLevel = 3;
    [ObservableProperty] private string _levelTitle = "Budget Apprentice";
    [ObservableProperty] private int _currentXp = 1250;
    [ObservableProperty] private int _requiredXp = 1750;
    [ObservableProperty] private ObservableCollection<ChallengeItem> _challenges;
    
    public ChallengesViewModel()
    {
        Challenges = new ObservableCollection<ChallengeItem>
        {
            new() { Name = "Log 3 expenses", Description = "Record three expense transactions", XpReward = 30, IsCompleted = true },
            new() { Name = "Stay under grocery budget", Description = "Keep grocery spending below limit", XpReward = 50, IsCompleted = false },
            new() { Name = "Review debt progress", Description = "Check your debt payoff plan", XpReward = 20, IsCompleted = false }
        };
    }
    
    public double LevelProgress => (double)_currentXp / _requiredXp;
}

public class ChallengeItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int XpReward { get; set; }
    public bool IsCompleted { get; set; }
}