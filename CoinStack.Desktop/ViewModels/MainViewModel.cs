// File: CoinStack.Desktop/ViewModels/MainViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoinStack.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private bool _isDrawerExpanded = true;
    
    public ObservableCollection<MenuItem> MenuItems { get; }
    
    public MainViewModel()
    {
        MenuItems =
        [
            new() { Name = "Dashboard", Icon = "📊", IconChar = "\ue1ac", ViewModel = new DashboardViewModel() },
            new() { Name = "Transactions", Icon = "💰", IconChar = "\ue1ad", ViewModel = new TransactionsViewModel() },
            new() { Name = "Budgets", Icon = "📁", IconChar = "\ue1c0", ViewModel = new BudgetsViewModel() },
            new() { Name = "Goals", Icon = "🎯", IconChar = "\ue1c1", ViewModel = new GoalsViewModel() },
            new() { Name = "Debt", Icon = "💳", IconChar = "\ue1c4", ViewModel = new DebtViewModel() },
            new() { Name = "Categories", Icon = "🏷️", IconChar = "\ue1c5", ViewModel = new CategoriesViewModel() },
            new() { Name = "Income", Icon = "📈", IconChar = "\ue1d0", ViewModel = new IncomeViewModel() },
            new() { Name = "Savings", Icon = "🏦", IconChar = "\ue1d5", ViewModel = new SavingsViewModel() },
            new() { Name = "Achievements", Icon = "🏆", IconChar = "\ue1d9", ViewModel = new AchievementsViewModel() },
            new() { Name = "Challenges", Icon = "⚡", IconChar = "\ue1da", ViewModel = new ChallengesViewModel() },
            new() { Name = "Reports", Icon = "📄", IconChar = "\ue1db", ViewModel = new ReportsViewModel() },
            new() { Name = "Subscriptions", Icon = "🔄", IconChar = "\ue1dc", ViewModel = new SubscriptionsViewModel() },
            new() { Name = "Settings", Icon = "⚙️", IconChar = "\ue1de", ViewModel = new SettingsViewModel() }
        ];
        
        CurrentPage = MenuItems[0].ViewModel;
    }
    
    [RelayCommand]
    private void Navigate(MenuItem item)
    {
        if (item?.ViewModel != null)
            CurrentPage = item.ViewModel;
    }
    
    [RelayCommand]
    private void ToggleDrawer()
    {
        IsDrawerExpanded = !IsDrawerExpanded;
    }
}

public class MenuItem
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string IconChar { get; set; } = "";
    public ViewModelBase ViewModel { get; set; } = null!;
}