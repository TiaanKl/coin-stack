using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace CoinStack.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;

    [ObservableProperty] private ViewModelBase? _currentPage;
    [ObservableProperty] private bool _isDrawerExpanded = true;
    [ObservableProperty] private NavItem? _selectedItem;

    public ObservableCollection<NavSection> Sections { get; } = [];

    public MainViewModel(IServiceProvider services)
    {
        _services = services;

        Sections.Add(new NavSection("Overview",
        [
            new NavItem("Dashboard", "fa-solid fa-house", typeof(DashboardViewModel)),
            new NavItem("Reports", "fa-solid fa-chart-line", typeof(ReportsViewModel)),
        ]));

        Sections.Add(new NavSection("Money In & Out",
        [
            new NavItem("Accounts", "fa-solid fa-building-columns", typeof(AccountsViewModel)),
            new NavItem("Transactions", "fa-solid fa-right-left", typeof(TransactionsViewModel)),
            new NavItem("Income", "fa-solid fa-arrow-trend-up", typeof(IncomeViewModel)),
            new NavItem("Categories", "fa-solid fa-tags", typeof(CategoriesViewModel)),
            new NavItem("Subscriptions", "fa-solid fa-rotate", typeof(SubscriptionsViewModel)),
        ]));

        Sections.Add(new NavSection("Planning",
        [
            new NavItem("Buckets", "fa-solid fa-wallet", typeof(BudgetsViewModel)),
            new NavItem("Goals", "fa-solid fa-bullseye", typeof(GoalsViewModel)),
            new NavItem("Savings", "fa-solid fa-piggy-bank", typeof(SavingsViewModel)),
        ]));

        Sections.Add(new NavSection("Debt",
        [
            new NavItem("Debt Accounts", "fa-solid fa-credit-card", typeof(DebtViewModel)),
        ]));

        Sections.Add(new NavSection("System",
        [
            new NavItem("Settings", "fa-solid fa-gear", typeof(SettingsViewModel)),
        ]));

        var first = Sections[0].Items[0];
        _ = NavigateAsync(first);
    }

    partial void OnSelectedItemChanged(NavItem? value)
    {
        foreach (var section in Sections)
        {
            foreach (var item in section.Items)
                item.IsSelected = ReferenceEquals(item, value);
        }
    }

    [RelayCommand]
    private async Task NavigateAsync(NavItem? item)
    {
        if (item is null) return;

        SelectedItem = item;
        var vm = (ViewModelBase)_services.GetRequiredService(item.ViewModelType);
        CurrentPage = vm;

        if (vm is IAsyncInitializable initializable)
            await initializable.InitializeAsync();
    }

    [RelayCommand]
    private void ToggleDrawer() => IsDrawerExpanded = !IsDrawerExpanded;
}

public interface IAsyncInitializable
{
    Task InitializeAsync();
}

public sealed class NavSection(string title, NavItem[] items)
{
    public string Title { get; } = title;
    public ObservableCollection<NavItem> Items { get; } = new(items);
}

public partial class NavItem : ObservableObject
{
    public NavItem(string name, string icon, Type viewModelType)
    {
        Name = name;
        Icon = icon;
        ViewModelType = viewModelType;
    }

    public string Name { get; }
    public string Icon { get; }
    public Type ViewModelType { get; }

    [ObservableProperty] private bool _isSelected;
}
