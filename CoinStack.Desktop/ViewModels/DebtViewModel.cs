// File: CoinStack.Desktop/ViewModels/DebtViewModel.cs

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class DebtViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<DebtAccount> _debts;
    [ObservableProperty] private decimal _principal;
    [ObservableProperty] private decimal _totalOwed;
    [ObservableProperty] private decimal _interestRate = 5.0m;
    [ObservableProperty] private decimal _monthlyPayment;
    [ObservableProperty] private int _termMonths = 24;
    
    public DebtViewModel()
    {
        Debts = new ObservableCollection<DebtAccount>
        {
            new() { Name = "Credit Card", Provider = "Chase", Balance = 2450, Total = 2500, InterestRate = 19.99m, MonthlyPayment = 100, StartDate = new DateTime(2025, 1, 1) },
            new() { Name = "Car Loan", Provider = "AutoBank", Balance = 12400, Total = 15000, InterestRate = 4.5m, MonthlyPayment = 350, StartDate = new DateTime(2024, 6, 1) }
        };
    }
}

public class DebtAccount
{
    public string Name { get; set; } = "";
    public string Provider { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal Total { get; set; }
    public decimal InterestRate { get; set; }
    public decimal MonthlyPayment { get; set; }
    public DateTime StartDate { get; set; }
    public double Progress => (double)(Balance / Total);
}