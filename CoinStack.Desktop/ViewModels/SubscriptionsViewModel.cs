// File: CoinStack.Desktop/ViewModels/SubscriptionsViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class SubscriptionsViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<SubscriptionItem> _subscriptions;
    
    public SubscriptionsViewModel()
    {
        Subscriptions = new ObservableCollection<SubscriptionItem>
        {
            new() { Service = "Netflix", Category = "Entertainment", Cycle = "Monthly", Cost = 15.99m, DebitDay = 15 },
            new() { Service = "Spotify", Category = "Entertainment", Cycle = "Monthly", Cost = 11.99m, DebitDay = 5 },
            new() { Service = "Gym", Category = "Health", Cycle = "Monthly", Cost = 49.99m, DebitDay = 1 }
        };
    }
}

public class SubscriptionItem
{
    public string Service { get; set; } = "";
    public string Category { get; set; } = "";
    public string Cycle { get; set; } = "";
    public decimal Cost { get; set; }
    public int DebitDay { get; set; }
}