using System;
using System.Collections.Generic;
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
    private readonly IAccountService _accounts;
    private readonly ICategoryService _categories;
    private readonly ISettingsService _settings;
    private readonly IMoneyFormatter _money;

    private string _currency = AppSettings.DefaultCurrency;

    [ObservableProperty] private string _newDescription = "";
    [ObservableProperty] private decimal _newAmount;
    [ObservableProperty] private TransactionType _newType = TransactionType.Expense;
    [ObservableProperty] private CategoryOption? _selectedCategory;
    [ObservableProperty] private AccountOption? _selectedAccount;
    [ObservableProperty] private AccountOption? _transferToAccount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTransfer))]
    private TransactionType _newTypeChoice = TransactionType.Expense;
    [ObservableProperty] private string _totalExpenseText = "";
    [ObservableProperty] private string _totalIncomeText = "";

    public ObservableCollection<TransactionRow> Transactions { get; } = [];
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = [];
    public ObservableCollection<AccountOption> AccountOptions { get; } = [];
    public ObservableCollection<TransactionType> TypeOptions { get; } =
        [TransactionType.Expense, TransactionType.Income, TransactionType.Transfer];

    /// <summary>A transfer moves money to another account instead of having a category.</summary>
    public bool IsTransfer => NewTypeChoice == TransactionType.Transfer;

    partial void OnNewTypeChoiceChanged(TransactionType value) => NewType = value;

    public TransactionsViewModel(
        ITransactionService transactions,
        IAccountService accounts,
        ICategoryService categories,
        ISettingsService settings,
        IMoneyFormatter money)
    {
        _transactions = transactions;
        _accounts = accounts;
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

            var accounts = (await _accounts.GetBalancesAsync()).Select(b => b.Account).ToList();
            _accountNames = accounts.ToDictionary(a => a.Id, a => a.Name);
            _mainAccountId = accounts.FirstOrDefault(a => a.IsDefault)?.Id;
            AccountOptions.Clear();
            foreach (var account in accounts)
                AccountOptions.Add(new AccountOption(account.Id, account.Name));
            SelectedAccount = AccountOptions.FirstOrDefault(o => o.Id == SelectedAccount?.Id)
                              ?? AccountOptions.FirstOrDefault(o => o.Id == _mainAccountId);

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

        if (IsTransfer && (TransferToAccount is null || TransferToAccount.Id == SelectedAccount?.Id))
        {
            StatusMessage = "Choose a different account for the money to go to.";
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
                AccountId = SelectedAccount?.Id,
                TransferAccountId = IsTransfer ? TransferToAccount?.Id : null,
                CategoryId = IsTransfer ? null : SelectedCategory?.Id,
                OccurredAtUtc = DateTime.UtcNow,
                Source = "Manual"
            });

            NewDescription = "";
            NewAmount = 0;
            NewTypeChoice = TransactionType.Expense;
            TransferToAccount = null;
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

    private Dictionary<int, string> _accountNames = [];
    private int? _mainAccountId;

    private string AccountName(int? id) => _accountNames.GetValueOrDefault(id ?? _mainAccountId ?? 0, "Archived account");

    private TransactionRow ToRow(Transaction tx) => new()
    {
        Id = tx.Id,
        Date = tx.OccurredAtUtc,
        Description = tx.Description,
        Category = tx.Type == TransactionType.Transfer
            ? $"{AccountName(tx.AccountId)} → {(tx.TransferAccountId is null ? "?" : AccountName(tx.TransferAccountId))}"
            : $"{tx.Category?.Name ?? "Uncategorized"} · {AccountName(tx.AccountId)}",
        TypeLabel = tx.Type.ToString(),
        AmountText = _money.Format(_currency, tx.Amount),
        IsIncome = tx.Type == TransactionType.Income
    };
}
