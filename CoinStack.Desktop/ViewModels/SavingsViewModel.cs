// File: CoinStack.Desktop/ViewModels/SavingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class SavingsViewModel : ViewModelBase
{
    [ObservableProperty] private decimal _totalSaved = 5280;
    [ObservableProperty] private decimal _available = 2080;
    [ObservableProperty] private decimal _thisMonth = 200;
    [ObservableProperty] private decimal _interestEarned = 45.20m;
    [ObservableProperty] private decimal _emergencyTotal = 3200;
    [ObservableProperty] private decimal _emergencyAvailable = 3200;
}