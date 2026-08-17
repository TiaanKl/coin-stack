// File: CoinStack.Desktop/ViewModels/TransactionsViewModel.cs

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CoinStack.Desktop.ViewModels;

public partial class TransactionsViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<TransactionItem> _transactions;
    
    public TransactionsViewModel()
    {
        Transactions = new ObservableCollection<TransactionItem>
        {
            new() { Date = DateTime.Now.AddDays(-1), Description = "Amazon", Amount = 129.99m, Type = "Expense", Category = "Shopping" },
            new() { Date = DateTime.Now.AddDays(-2), Description = "Uber", Amount = 22.50m, Type = "Expense", Category = "Transport" },
            new() { Date = DateTime.Now.AddDays(-3), Description = "Salary Oct", Amount = 3400m, Type = "Income", Category = "Salary" },
            new() { Date = DateTime.Now.AddDays(-4), Description = "Electric Bill", Amount = 95.40m, Type = "Expense", Category = "Utilities" },
            new() { Date = DateTime.Now.AddDays(-5), Description = "Freelance", Amount = 450m, Type = "Income", Category = "Extra" }
        };
    }
}