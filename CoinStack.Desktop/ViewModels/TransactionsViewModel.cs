using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoinStack.Desktop.ViewModels;

public partial class TransactionsViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ITransactionService _transactions;
    private readonly ICategoryService _categories;
    private readonly ISettingsService _settings;
    private readonly IMoneyFormatter _money;

    private string _currency = AppSettings.DefaultCurrency;

    [ObservableProperty] private string _newDescription = "";
    [ObservableProperty] private decimal _newAmount;
    [ObservableProperty] private TransactionType _newType = TransactionType.Expense;
    [ObservableProperty] private CategoryOption? _selectedCategory;
    [ObservableProperty] private string _totalExpenseText = "";
    [ObservableProperty] private string _totalIncomeText = "";

    public ObservableCollection<TransactionRow> Transactions { get; } = [];
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = [];
    public ObservableCollection<TransactionType> TypeOptions { get; } =
        [TransactionType.Expense, TransactionType.Income];

    public TransactionsViewModel(
        ITransactionService transactions,
        ICategoryService categories,
        ISettingsService settings,
        IMoneyFormatter money)
    {
        _transactions = transactions;
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
            _currency = settings.Currency;

            CategoryOptions.Clear();
            foreach (var cat in (await _categories.GetAllAsync()).OrderBy(c => c.Name))
                CategoryOptions.Add(new CategoryOption(cat.Id, cat.Name));
            SelectedCategory ??= CategoryOptions.FirstOrDefault();

            var (lifetimeIncome, lifetimeExpense) = await _transactions.GetLifetimeTotalsAsync();
            TotalIncomeText = _money.Format(_currency, lifetimeIncome);
            TotalExpenseText = _money.Format(_currency, lifetimeExpense);

            Transactions.Clear();
            foreach (var tx in await _transactions.GetAllAsync())
                Transactions.Add(ToRow(tx));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load transactions: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddTransactionAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDescription) || NewAmount <= 0)
        {
            StatusMessage = "Enter a description and a positive amount.";
            return;
        }

        IsBusy = true;
        try
        {
            await _transactions.CreateAsync(new Transaction
            {
                Description = NewDescription.Trim(),
                Amount = NewAmount,
                Type = NewType,
                CategoryId = SelectedCategory?.Id,
                OccurredAtUtc = DateTime.UtcNow,
                Source = "Manual"
            });

            NewDescription = "";
            NewAmount = 0;
            NewType = TransactionType.Expense;
            SelectedCategory = CategoryOptions.FirstOrDefault();
            StatusMessage = "Transaction added.";
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not add transaction: {ex.Message}";
            IsBusy = false;
        }
    }

    private TransactionRow ToRow(Transaction tx) => new()
    {
        Id = tx.Id,
        Date = tx.OccurredAtUtc,
        Description = tx.Description,
        Category = tx.Category?.Name ?? "Uncategorized",
        TypeLabel = tx.Type.ToString(),
        AmountText = _money.Format(_currency, tx.Amount),
        IsIncome = tx.Type == TransactionType.Income
    };
}
