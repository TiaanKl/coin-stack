// File: CoinStack.Desktop/ViewModels/BudgetsViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class BudgetsViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<BudgetBucket> _buckets;
    
    public BudgetsViewModel()
    {
        Buckets = new ObservableCollection<BudgetBucket>
        {
            new() { Name = "Groceries", Planned = 500, Spent = 412.30m, Remaining = 87.70m, Color = "#10B981" },
            new() { Name = "Entertainment", Planned = 200, Spent = 178.90m, Remaining = 21.10m, Color = "#F59E0B" },
            new() { Name = "Transport", Planned = 300, Spent = 290.00m, Remaining = 10.00m, Color = "#6366F1" },
            new() { Name = "Dining Out", Planned = 250, Spent = 310.50m, Remaining = -60.50m, Color = "#EF4444" }
        };
    }
}

public class BudgetBucket
{
    public string Name { get; set; } = "";
    public decimal Planned { get; set; }
    public decimal Spent { get; set; }
    public decimal Remaining { get; set; }
    public string Color { get; set; } = "";
}