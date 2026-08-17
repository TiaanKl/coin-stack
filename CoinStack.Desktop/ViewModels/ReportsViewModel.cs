// File: CoinStack.Desktop/ViewModels/ReportsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    [ObservableProperty] private decimal _incomeThisMonth = 3400;
    [ObservableProperty] private decimal _expensesThisMonth = 2260;
    [ObservableProperty] private decimal _netCashflow = 1140;
    [ObservableProperty] private decimal _savingsAvailable = 2080;
}