using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CoinStack.Data.Entities;
using CoinStack.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CoinStack.Desktop.ViewModels;

/// <summary>Where the money is: each account's balance, updating a balance, and moving money between accounts.</summary>
public partial class AccountsViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IAccountService _accounts;
    private readonly ISettingsService _settings;
    private readonly IMoneyFormatter _money;

    private string _currency = AppSettings.DefaultCurrency;

    [ObservableProperty] private string _spendableText = "";
    [ObservableProperty] private string _totalText = "";

    [ObservableProperty] private AccountOption? _balanceAccount;
    [ObservableProperty] private decimal _newBalance;

    [ObservableProperty] private AccountOption? _transferFrom;
    [ObservableProperty] private AccountOption? _transferTo;
    [ObservableProperty] private decimal _transferAmount;

    [ObservableProperty] private string _newAccountName = "";
    [ObservableProperty] private AccountType _newAccountType = AccountType.Cheque;
    [ObservableProperty] private decimal _newAccountBalance;

    public ObservableCollection<AccountRow> Accounts { get; } = [];
    public ObservableCollection<AccountOption> AccountOptions { get; } = [];
    public ObservableCollection<AccountType> TypeOptions { get; } = [.. Enum.GetValues<AccountType>()];

    public AccountsViewModel(IAccountService accounts, ISettingsService settings, IMoneyFormatter money)
    {
        _accounts = accounts;
        _settings = settings;
        _money = money;
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            _currency = (await _settings.GetAsync()).Currency;
            var balances = await _accounts.GetBalancesAsync();

            Accounts.Clear();
            AccountOptions.Clear();
            foreach (var entry in balances)
            {
                var account = entry.Account;
                var number = AccountDisplay.MaskedNumber(account.AccountNumber);
                Accounts.Add(new AccountRow
                {
                    Id = account.Id,
                    Name = account.Name,
                    Details = AccountDisplay.Label(account.Type) + (number is null ? "" : $" · {number}") + (account.IsDefault ? " · Main" : ""),
                    BalanceText = _money.Format(_currency, entry.Balance),
                    IsNegative = entry.Balance < 0,
                    AsOfText = account.BalanceAsOfUtc is { } asOf
                        ? $"From {_money.Format(_currency, account.Balance)} on {asOf:dd MMM yyyy}, plus everything since"
                        : "No balance entered yet"
                });
                AccountOptions.Add(new AccountOption(account.Id, account.Name));
            }

            SpendableText = _money.Format(_currency, balances.Where(b => AccountService.IsSpendable(b.Account)).Sum(b => b.Balance));
            TotalText = _money.Format(_currency, balances.Sum(b => b.Balance));

            BalanceAccount = AccountOptions.FirstOrDefault(o => o.Id == BalanceAccount?.Id) ?? AccountOptions.FirstOrDefault();
            TransferFrom = AccountOptions.FirstOrDefault(o => o.Id == TransferFrom?.Id) ?? AccountOptions.FirstOrDefault();
            TransferTo = AccountOptions.FirstOrDefault(o => o.Id == TransferTo?.Id) ?? AccountOptions.Skip(1).FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load accounts: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task UpdateBalanceAsync() => RunAsync(async () =>
    {
        if (BalanceAccount is null)
        {
            return "Choose an account.";
        }

        await _accounts.SetBalanceAsync(BalanceAccount.Id, NewBalance, DateTime.UtcNow);
        return $"{BalanceAccount.Name} set to {_money.Format(_currency, NewBalance)}.";
    });

    [RelayCommand]
    private Task TransferAsync() => RunAsync(async () =>
    {
        if (TransferFrom is null || TransferTo is null)
        {
            return "Choose both accounts.";
        }

        await _accounts.TransferAsync(TransferFrom.Id, TransferTo.Id, TransferAmount, DateTime.UtcNow);
        var message = $"Moved {_money.Format(_currency, TransferAmount)} to {TransferTo.Name}.";
        TransferAmount = 0;
        return message;
    });

    [RelayCommand]
    private Task AddAccountAsync() => RunAsync(async () =>
    {
        await _accounts.CreateAsync(new Account
        {
            Name = NewAccountName,
            Type = NewAccountType,
            Balance = NewAccountBalance,
            BalanceAsOfUtc = DateTime.UtcNow
        });

        var message = $"{NewAccountName.Trim()} added.";
        NewAccountName = "";
        NewAccountBalance = 0;
        return message;
    });

    /// <summary>Runs a change, then reloads; a refused change shows why.</summary>
    private async Task RunAsync(Func<Task<string>> change)
    {
        IsBusy = true;
        try
        {
            var message = await change();
            await InitializeAsync();
            StatusMessage = message;
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
