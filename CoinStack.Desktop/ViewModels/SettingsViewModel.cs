// File: CoinStack.Desktop/ViewModels/SettingsViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private string _selectedCurrency = "USD";
    [ObservableProperty] private int _monthlyStartDay = 1;
    [ObservableProperty] private decimal _monthlyNetIncome = 3400;
    [ObservableProperty] private bool _reserveAwareBudget = true;
    [ObservableProperty] private bool _emergencyFundFallback = true;
    
    public ObservableCollection<string> Currencies { get; } = new() { "USD", "EUR", "GBP", "CAD", "AUD", "ZAR" };
    public ObservableCollection<int> StartDays { get; } = new() { 1, 5, 10, 15, 20, 25, 28 };
}