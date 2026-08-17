// File: CoinStack.Desktop/ViewModels/GoalsViewModel.cs

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class GoalsViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<GoalItem> _goals;
    
    public GoalsViewModel()
    {
        Goals = new ObservableCollection<GoalItem>
        {
            new() { Name = "Emergency Fund", Target = 10000, Current = 3200, TargetDate = DateTime.Now.AddMonths(8) },
            new() { Name = "Vacation", Target = 3000, Current = 1250, TargetDate = DateTime.Now.AddMonths(3) },
            new() { Name = "New Laptop", Target = 1500, Current = 1500, TargetDate = DateTime.Now, IsCompleted = true }
        };
    }
}

public class GoalItem
{
    public string Name { get; set; } = "";
    public decimal Target { get; set; }
    public decimal Current { get; set; }
    public DateTime? TargetDate { get; set; }
    public bool IsCompleted { get; set; }
    public double Progress => (double)(Current / Target);
}