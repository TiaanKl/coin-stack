// File: CoinStack.Desktop/ViewModels/AchievementsViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class AchievementsViewModel : ViewModelBase
{
    [ObservableProperty] private int _currentLevel = 3;
    [ObservableProperty] private string _levelTitle = "Budget Apprentice";
    [ObservableProperty] private int _totalXp = 1250;
    [ObservableProperty] private int _xpToNext = 500;
    [ObservableProperty] private int _achievementsUnlocked = 7;
    [ObservableProperty] private int _totalAchievements = 24;
    [ObservableProperty] private ObservableCollection<AchievementItem> _achievements;
    
    public AchievementsViewModel()
    {
        Achievements = new ObservableCollection<AchievementItem>
        {
            new() { Name = "First Transaction", Description = "Log your first transaction", IsUnlocked = true, XpReward = 50 },
            new() { Name = "Budget Master", Description = "Stay under budget for 3 months", IsUnlocked = false, XpReward = 200 },
            new() { Name = "Debt Destroyer", Description = "Pay off a debt account", IsUnlocked = true, XpReward = 150 }
        };
    }
    
    public double LevelProgress => (double)_totalXp / (_totalXp + _xpToNext);
}

public class AchievementItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsUnlocked { get; set; }
    public int XpReward { get; set; }
    public string UnlockStatusText => IsUnlocked ? "Unlocked: Yes" : "Unlocked: No";
    public string UnlockStatusForeground => IsUnlocked ? "#10B981" : "#6B7280";
}