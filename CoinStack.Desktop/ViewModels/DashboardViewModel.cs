// File: CoinStack.Desktop/ViewModels/DashboardViewModel.cs
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    [ObservableProperty] private decimal _budgetRemaining = 1840;
    [ObservableProperty] private decimal _budgetSpent = 660;
    [ObservableProperty] private decimal _budgetLimit = 2500;
    [ObservableProperty] private decimal _reservesTotal = 5280;
    [ObservableProperty] private decimal _emergencyFund = 3200;
    
    public ObservableCollection<TransactionItem> RecentTransactions { get; }
    public ObservableCollection<ScoreEventItem> RecentScoreEvents { get; }
    
    public DashboardViewModel()
    {
        RecentTransactions = new ObservableCollection<TransactionItem>
        {
            new() { Date = DateTime.Now.AddDays(-1), Description = "Grocery Store", Amount = 84.50m, Type = "Expense" },
            new() { Date = DateTime.Now.AddDays(-2), Description = "Salary", Amount = 3200m, Type = "Income" },
            new() { Date = DateTime.Now.AddDays(-3), Description = "Netflix", Amount = 15.99m, Type = "Expense" },
            new() { Date = DateTime.Now.AddDays(-4), Description = "Restaurant", Amount = 46.20m, Type = "Expense" },
            new() { Date = DateTime.Now.AddDays(-5), Description = "Freelance", Amount = 500m, Type = "Income" }
        };
        
        RecentScoreEvents = new ObservableCollection<ScoreEventItem>
        {
            new() { Event = "Budget under target", Points = 15, IsPositive = true },
            new() { Event = "Impulse purchase detected", Points = -5, IsPositive = false },
            new() { Event = "Completed daily challenge", Points = 10, IsPositive = true },
            new() { Event = "Paid debt on time", Points = 20, IsPositive = true }
        };
    }
}

public class TransactionItem
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Type { get; set; } = "";
    public string Category { get; set; } = "";
    public string TransactionForeground => Type == "Income" ? "#10B981" : "#EF4444";
}

public class ScoreEventItem
{
    public string Event { get; set; } = "";
    public int Points { get; set; }
    public bool IsPositive { get; set; }
    public string PointsForeground => IsPositive ? "#10B981" : "#EF4444";
}