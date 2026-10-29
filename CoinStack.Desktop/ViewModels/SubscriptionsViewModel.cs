using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoinStack.Desktop.ViewModels;

public partial class SubscriptionsViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ISubscriptionService _subscriptions;
    private readonly ICategoryService _categories;
    private readonly ISettingsService _settings;
    private readonly IMoneyFormatter _money;

    [ObservableProperty] private string _monthlyTotalText = "";
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private CategoryOption? _selectedCategory;
    [ObservableProperty] private decimal _newCost;
    [ObservableProperty] private int? _newDebitDay = 1;
    [ObservableProperty] private SubscriptionCycle _newCycle = SubscriptionCycle.Monthly;
    [ObservableProperty] private SubscriptionStatus _newStatus = SubscriptionStatus.Active;
    [ObservableProperty] private bool _hasSubscriptions;

    public ObservableCollection<SubscriptionRow> Subscriptions { get; } = [];
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = [];
    public ObservableCollection<SubscriptionCycle> CycleOptions { get; } =
        [SubscriptionCycle.Weekly, SubscriptionCycle.Monthly, SubscriptionCycle.Quarterly, SubscriptionCycle.Yearly];
    public ObservableCollection<SubscriptionStatus> StatusOptions { get; } =
        [SubscriptionStatus.Active, SubscriptionStatus.Paused, SubscriptionStatus.Cancelled];

    public SubscriptionsViewModel(
        ISubscriptionService subscriptions,
        ICategoryService categories,
        ISettingsService settings,
        IMoneyFormatter money)
    {
        _subscriptions = subscriptions;
        _categories = categories;
        _settings = settings;
        _money = money;
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var settings = await _settings.GetAsync();
            var currency = settings.Currency;

            CategoryOptions.Clear();
            foreach (var cat in (await _categories.GetAllAsync()).OrderBy(c => c.Name))
                CategoryOptions.Add(new CategoryOption(cat.Id, cat.Name));

            SelectedCategory ??= CategoryOptions.FirstOrDefault();

            var list = await _subscriptions.GetAllAsync();
            Subscriptions.Clear();
            foreach (var sub in list.OrderBy(s => s.Name))
            {
                Subscriptions.Add(new SubscriptionRow
                {
                    Id = sub.Id,
                    Name = sub.Name,
                    Category = string.IsNullOrWhiteSpace(sub.Category) ? "General" : sub.Category,
                    CycleText = sub.Cycle.ToString(),
                    CostText = _money.Format(currency, sub.Cost),
                    StatusText = sub.Status.ToString(),
                    DebitDayText = sub.DebitOrderDay is int day ? $"Day {day}" : "—"
                });
            }

            var active = list.Where(s => s.Status == SubscriptionStatus.Active).ToList();
            ActiveCount = active.Count;
            HasSubscriptions = Subscriptions.Count > 0;
            MonthlyTotalText = _money.Format(currency, active
                .Sum(s => s.Cycle switch
                {
                    SubscriptionCycle.Weekly => s.Cost * 52m / 12m,
                    SubscriptionCycle.Monthly => s.Cost,
                    SubscriptionCycle.Quarterly => s.Cost / 3m,
                    SubscriptionCycle.Yearly => s.Cost / 12m,
                    _ => s.Cost
                }));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load subscriptions: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddSubscriptionAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName) || NewCost < 0)
        {
            StatusMessage = "Enter a name and cost.";
            return;
        }

        IsBusy = true;
        try
        {
            await _subscriptions.CreateAsync(new Subscription
            {
                Name = NewName.Trim(),
                Category = SelectedCategory?.Name ?? "General",
                Cost = NewCost,
                Cycle = NewCycle,
                DebitOrderDay = NewDebitDay,
                Status = NewStatus
            });

            NewName = "";
            NewCost = 0;
            NewDebitDay = 1;
            NewCycle = SubscriptionCycle.Monthly;
            NewStatus = SubscriptionStatus.Active;
            SelectedCategory = CategoryOptions.FirstOrDefault();
            StatusMessage = "Subscription added.";
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not add subscription: {ex.Message}";
            IsBusy = false;
        }
    }
}
