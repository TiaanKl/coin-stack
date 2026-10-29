using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoinStack.Desktop.ViewModels;

public partial class BudgetsViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IBucketService _buckets;
    private readonly ICategoryService _categories;
    private readonly IGoalService _goals;
    private readonly ISettingsService _settings;
    private readonly IMoneyFormatter _money;

    [ObservableProperty] private string _totalAllocatedText = "";
    [ObservableProperty] private string _totalSpentText = "";
    [ObservableProperty] private string _totalRemainingText = "";
    [ObservableProperty] private string _newBucketName = "";
    [ObservableProperty] private decimal _newAllocatedAmount;
    [ObservableProperty] private CategoryOption? _selectedCategory;
    [ObservableProperty] private GoalOption? _selectedGoal;
    [ObservableProperty] private bool _isSavingsBucket;
    [ObservableProperty] private bool _hasBuckets;

    public ObservableCollection<BucketRow> Buckets { get; } = [];
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = [];
    public ObservableCollection<GoalOption> GoalOptions { get; } = [];

    public BudgetsViewModel(
        IBucketService buckets,
        ICategoryService categories,
        IGoalService goals,
        ISettingsService settings,
        IMoneyFormatter money)
    {
        _buckets = buckets;
        _categories = categories;
        _goals = goals;
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
            var now = DateTime.UtcNow;

            CategoryOptions.Clear();
            CategoryOptions.Add(new CategoryOption(0, "No category"));
            foreach (var cat in (await _categories.GetAllAsync()).OrderBy(c => c.Name))
                CategoryOptions.Add(new CategoryOption(cat.Id, cat.Name));
            SelectedCategory ??= CategoryOptions.FirstOrDefault();

            GoalOptions.Clear();
            GoalOptions.Add(new GoalOption(0, "No goal"));
            foreach (var goal in (await _goals.GetAllAsync()).Where(g => g.Status == GoalStatus.Active).OrderBy(g => g.Name))
                GoalOptions.Add(new GoalOption(goal.Id, goal.Name));
            SelectedGoal ??= GoalOptions.FirstOrDefault();

            var buckets = await _buckets.GetAllAsync();
            var spentMap = await _buckets.GetSpentAmountsForPeriodAsync(settings.MonthStartDay, now);

            Buckets.Clear();
            decimal totalAllocated = 0;
            decimal totalSpent = 0;

            foreach (var bucket in buckets.OrderBy(b => b.SortOrder).ThenBy(b => b.Name))
            {
                var spent = spentMap.TryGetValue(bucket.Id, out var s) ? s : 0m;
                var remaining = bucket.AllocatedAmount - spent;
                var over = spent > bucket.AllocatedAmount && bucket.AllocatedAmount > 0;
                var pct = bucket.AllocatedAmount > 0
                    ? Math.Min(100, (double)(spent / bucket.AllocatedAmount * 100m))
                    : 0;

                totalAllocated += bucket.AllocatedAmount;
                totalSpent += spent;

                Buckets.Add(new BucketRow
                {
                    Id = bucket.Id,
                    Name = bucket.Name,
                    CategoryText = bucket.Category?.Name ?? "Uncategorized",
                    GoalText = bucket.Goal?.Name,
                    AllocatedText = _money.Format(currency, bucket.AllocatedAmount),
                    SpentText = _money.Format(currency, spent),
                    RemainingText = _money.Format(currency, remaining),
                    ProgressValue = (double)spent,
                    ProgressMaximum = bucket.AllocatedAmount > 0 ? (double)bucket.AllocatedAmount : 1,
                    ProgressPercentText = $"{pct:0}%",
                    IsOverBudget = over,
                    IsSavings = bucket.IsSavings,
                    StatusText = over ? "Over budget" : bucket.IsSavings ? "Savings" : "On track"
                });
            }

            HasBuckets = Buckets.Count > 0;
            TotalAllocatedText = _money.Format(currency, totalAllocated);
            TotalSpentText = _money.Format(currency, totalSpent);
            TotalRemainingText = _money.Format(currency, totalAllocated - totalSpent);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load buckets: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddBucketAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBucketName) || NewAllocatedAmount < 0)
        {
            StatusMessage = "Enter a bucket name and allocated amount.";
            return;
        }

        IsBusy = true;
        try
        {
            await _buckets.CreateAsync(new Bucket
            {
                Name = NewBucketName.Trim(),
                AllocatedAmount = NewAllocatedAmount,
                CategoryId = SelectedCategory is { Id: > 0 } cat ? cat.Id : null,
                GoalId = SelectedGoal is { Id: > 0 } goal ? goal.Id : null,
                IsSavings = IsSavingsBucket
            });

            NewBucketName = "";
            NewAllocatedAmount = 0;
            IsSavingsBucket = false;
            SelectedCategory = CategoryOptions.FirstOrDefault();
            SelectedGoal = GoalOptions.FirstOrDefault();
            StatusMessage = "Bucket created.";
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not create bucket: {ex.Message}";
            IsBusy = false;
        }
    }
}
