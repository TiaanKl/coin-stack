// File: CoinStack.Desktop/ViewModels/IncomeViewModel.cs

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class IncomeViewModel : ViewModelBase
{
    [ObservableProperty] private decimal _receivedThisMonth = 3400;
    [ObservableProperty] private decimal _yearToDate = 28500;
    [ObservableProperty] private ObservableCollection<IncomeSource> _incomeSources;
    [ObservableProperty] private ObservableCollection<TransactionItem> _recentDeposits;
    
    public IncomeViewModel()
    {
        IncomeSources = new ObservableCollection<IncomeSource>
        {
            new() { Category = "Salary", Amount = 3200, Percentage = 85 },
            new() { Category = "Freelance", Amount = 450, Percentage = 12 },
            new() { Category = "Investments", Amount = 120, Percentage = 3 }
        };
        
        RecentDeposits = new ObservableCollection<TransactionItem>
        {
            new() { Date = DateTime.Now.AddDays(-2), Description = "Monthly Salary", Amount = 3200, Type = "Income" },
            new() { Date = DateTime.Now.AddDays(-5), Description = "Freelance Project", Amount = 450, Type = "Income" },
            new() { Date = DateTime.Now.AddDays(-10), Description = "Dividend", Amount = 45.30m, Type = "Income" }
        };
    }
}

public class IncomeSource
{
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public double Percentage { get; set; }
}