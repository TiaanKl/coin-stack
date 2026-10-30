using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

/// <summary>
/// Accounts and the money in them. An account's balance is its anchor (<see cref="Account.Balance"/> at
/// <see cref="Account.BalanceAsOfUtc"/>) moved by every transaction on it after that moment: income adds,
/// spending subtracts, and a transfer moves money from one account to the other.
/// </summary>
public sealed class AccountService : IAccountService
{
    public const string DefaultAccountName = "Main account";

    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly TimeProvider _clock;

    public AccountService(IDbContextFactory<CoinStackDbContext> dbFactory, TimeProvider? clock = null)
    {
        _dbFactory = dbFactory;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<List<AccountBalance>> GetBalancesAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await GetOrCreateDefaultAsync(db, cancellationToken);

        var balances = await GetBalancesAsync(db, cancellationToken);
        return includeArchived ? balances : balances.Where(b => !b.Account.IsArchived).ToList();
    }

    public async Task<Account> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await GetOrCreateDefaultAsync(db, cancellationToken);
    }

    public async Task<decimal> GetSpendableBalanceAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await GetSpendableBalanceAsync(db, cancellationToken);
    }

    public async Task<Dictionary<DateTime, decimal>> GetDailySpendableChangeAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var accounts = await db.Accounts.AsNoTracking().ToListAsync(cancellationToken);
        var defaultId = accounts.Where(a => a.IsDefault).Select(a => (int?)a.Id).FirstOrDefault();
        var spendable = accounts.Where(IsSpendable).Select(a => a.Id).ToHashSet();

        var rows = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc >= startUtc && t.OccurredAtUtc < endUtc)
            .Select(t => new Movement(t.AccountId, t.TransferAccountId, t.Type, t.Amount, t.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        // Grouped by the device's calendar day, which SQLite can't work out, so this happens in memory.
        return rows
            .GroupBy(r => _clock.LocalDate(r.OccurredAtUtc))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(r => SpendableChange(r, defaultId, spendable)));
    }

    public async Task<Account> CreateAsync(Account account, CancellationToken cancellationToken = default)
    {
        var name = account.Name.Trim();
        if (name.Length == 0)
        {
            throw new InvalidOperationException("Give the account a name.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        account.Name = name;
        account.AccountNumber = NormalizeAccountNumberText(account.AccountNumber);
        account.IsArchived = false;

        // Only an account nobody has entered a balance for yet may go without a date; every other account
        // needs one, or the transfers into it would never count.
        account.BalanceAsOfUtc ??= _clock.GetUtcNow().UtcDateTime;

        var hasDefault = await db.Accounts.AnyAsync(a => a.IsDefault && !a.IsArchived, cancellationToken);
        account.IsDefault = !hasDefault && account.Type == AccountType.Cheque;
        account.SortOrder = (await db.Accounts.MaxAsync(a => (int?)a.SortOrder, cancellationToken) ?? 0) + 1;

        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        var name = account.Name.Trim();
        if (name.Length == 0)
        {
            throw new InvalidOperationException("Give the account a name.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Accounts.FirstOrDefaultAsync(a => a.Id == account.Id, cancellationToken)
                       ?? throw new InvalidOperationException("That account no longer exists.");

        if (existing.IsDefault && account.Type != AccountType.Cheque)
        {
            throw new InvalidOperationException("The main account has to stay a cheque account. Make another account the main one first.");
        }

        existing.Name = name;
        existing.Type = account.Type;
        existing.AccountNumber = NormalizeAccountNumberText(account.AccountNumber);
        existing.SortOrder = account.SortOrder;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetArchivedAsync(int accountId, bool archived, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
                      ?? throw new InvalidOperationException("That account no longer exists.");

        if (archived && account.IsDefault)
        {
            throw new InvalidOperationException("The main account can't be archived. Make another account the main one first.");
        }

        account.IsArchived = archived;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetDefaultAsync(int accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
                      ?? throw new InvalidOperationException("That account no longer exists.");

        if (account.Type != AccountType.Cheque || account.IsArchived)
        {
            throw new InvalidOperationException("Only an open cheque account can be the main account.");
        }

        var previous = await db.Accounts.Where(a => a.IsDefault && a.Id != accountId).ToListAsync(cancellationToken);
        if (previous.Count > 0)
        {
            // A row with no account counts as the main account's; pin such rows to it before the role moves.
            var previousId = previous.Min(a => a.Id);
            await db.Transactions
                .Where(t => t.AccountId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.AccountId, previousId), cancellationToken);
        }

        foreach (var other in previous)
        {
            other.IsDefault = false;
        }

        account.IsDefault = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetBalanceAsync(int accountId, decimal balance, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
                      ?? throw new InvalidOperationException("That account no longer exists.");

        account.Balance = balance;
        account.BalanceAsOfUtc = asOfUtc;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Transaction> TransferAsync(
        int fromAccountId,
        int toAccountId,
        decimal amount,
        DateTime occurredAtUtc,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0m)
        {
            throw new InvalidOperationException("Enter an amount greater than zero.");
        }

        if (fromAccountId == toAccountId)
        {
            throw new InvalidOperationException("Choose two different accounts.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var accounts = await db.Accounts
            .Where(a => a.Id == fromAccountId || a.Id == toAccountId)
            .ToListAsync(cancellationToken);

        var from = accounts.FirstOrDefault(a => a.Id == fromAccountId);
        var to = accounts.FirstOrDefault(a => a.Id == toAccountId);
        if (from is null || to is null || from.IsArchived || to.IsArchived)
        {
            throw new InvalidOperationException("Both accounts have to be open.");
        }

        var transfer = AddTransfer(db, from.Id, to.Id, amount, occurredAtUtc,
            string.IsNullOrWhiteSpace(description) ? $"Transfer to {to.Name}" : description.Trim());
        await db.SaveChangesAsync(cancellationToken);
        return transfer;
    }

    /// <summary>Adds a transfer row to <paramref name="db"/> without saving, for callers moving money inside their own save.</summary>
    public static Transaction AddTransfer(
        CoinStackDbContext db,
        int fromAccountId,
        int toAccountId,
        decimal amount,
        DateTime occurredAtUtc,
        string description,
        string source = "Manual")
    {
        var transfer = new Transaction
        {
            Type = TransactionType.Transfer,
            AccountId = fromAccountId,
            TransferAccountId = toAccountId,
            Amount = amount,
            OccurredAtUtc = occurredAtUtc,
            Description = description,
            Source = source,
            ExpenseKind = ExpenseKind.Discretionary
        };

        db.Transactions.Add(transfer);
        return transfer;
    }

    /// <summary>
    /// The main account, tracked by <paramref name="db"/>. With none, an open cheque account takes the role,
    /// or a new "Main account" is made with no balance entered yet.
    /// </summary>
    public static async Task<Account> GetOrCreateDefaultAsync(CoinStackDbContext db, CancellationToken cancellationToken)
    {
        var current = await db.Accounts
            .Where(a => a.IsDefault && !a.IsArchived)
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (current is not null)
        {
            return current;
        }

        var cheque = await db.Accounts
            .Where(a => a.Type == AccountType.Cheque && !a.IsArchived)
            .OrderBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (cheque is not null)
        {
            cheque.IsDefault = true;
            await db.SaveChangesAsync(cancellationToken);
            return cheque;
        }

        var created = new Account
        {
            Name = DefaultAccountName,
            Type = AccountType.Cheque,
            IsDefault = true
        };
        db.Accounts.Add(created);
        await db.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <summary>
    /// Fills in the account a row belongs to and checks a transfer before it is saved. A transfer needs two
    /// different accounts and carries no bucket, debt or spending details. A transfer saved before accounts
    /// existed has no destination; it can still be edited and goes on moving nothing.
    /// </summary>
    public static async Task PrepareForSaveAsync(
        CoinStackDbContext db,
        Transaction transaction,
        Transaction? before,
        CancellationToken cancellationToken)
    {
        transaction.AccountId ??= (await GetOrCreateDefaultAsync(db, cancellationToken)).Id;

        if (transaction.Type != TransactionType.Transfer)
        {
            transaction.TransferAccountId = null;
            return;
        }

        var olderTransfer = before is { Type: TransactionType.Transfer, TransferAccountId: null };
        if (transaction.TransferAccountId is null && !olderTransfer)
        {
            throw new InvalidOperationException("Choose the account the money goes to.");
        }

        if (transaction.TransferAccountId == transaction.AccountId)
        {
            throw new InvalidOperationException("Choose two different accounts.");
        }

        transaction.BucketId = null;
        transaction.DebtAccountId = null;
        transaction.SubscriptionId = null;
        transaction.ExpenseKind = ExpenseKind.Discretionary;
        transaction.AutoDeduct = false;
        transaction.IsImpulse = false;
    }

    public static async Task<decimal> GetSpendableBalanceAsync(CoinStackDbContext db, CancellationToken cancellationToken)
    {
        var balances = await GetBalancesAsync(db, cancellationToken);
        return balances.Where(b => IsSpendable(b.Account)).Sum(b => b.Balance);
    }

    /// <summary>Every account's balance now, the main account first, then by type and the order chosen.</summary>
    public static async Task<List<AccountBalance>> GetBalancesAsync(CoinStackDbContext db, CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts.AsNoTracking().ToListAsync(cancellationToken);
        if (accounts.Count == 0)
        {
            return [];
        }

        var totals = accounts.ToDictionary(a => a.Id, a => a.Balance);
        var anchors = accounts
            .Where(a => a.BalanceAsOfUtc.HasValue)
            .ToDictionary(a => a.Id, a => a.BalanceAsOfUtc!.Value);

        if (anchors.Count > 0)
        {
            var defaultId = accounts.Where(a => a.IsDefault).Select(a => (int?)a.Id).FirstOrDefault();
            var earliest = anchors.Values.Min();

            var rows = await db.Transactions
                .AsNoTracking()
                .Where(t => t.OccurredAtUtc > earliest)
                .Select(t => new Movement(t.AccountId, t.TransferAccountId, t.Type, t.Amount, t.OccurredAtUtc))
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                foreach (var (accountId, change) in Changes(row, defaultId))
                {
                    if (anchors.TryGetValue(accountId, out var asOf) && row.OccurredAtUtc > asOf)
                    {
                        totals[accountId] += change;
                    }
                }
            }
        }

        return accounts
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Type)
            .ThenBy(a => a.SortOrder)
            .ThenBy(a => a.Id)
            .Select(a => new AccountBalance(a, totals[a.Id]))
            .ToList();
    }

    /// <summary>Money you can spend sits in open cheque accounts.</summary>
    public static bool IsSpendable(Account account) => account.Type == AccountType.Cheque && !account.IsArchived;

    /// <summary>Digits only, so "62 1234 5678", "621234-5678" and "6212345678" are the same account.</summary>
    public static string? AccountNumberDigits(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            return null;
        }

        var digits = new string(accountNumber.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static string? NormalizeAccountNumberText(string? accountNumber)
        => string.IsNullOrWhiteSpace(accountNumber) ? null : accountNumber.Trim();

    private static decimal SpendableChange(Movement row, int? defaultId, HashSet<int> spendable)
        => Changes(row, defaultId).Where(c => spendable.Contains(c.AccountId)).Sum(c => c.Change);

    /// <summary>
    /// How a row moves each account it touches. A row with no account is on the main account. A transfer
    /// with no destination predates accounts and moves nothing, as it always did.
    /// </summary>
    private static IEnumerable<(int AccountId, decimal Change)> Changes(Movement row, int? defaultId)
    {
        if ((row.AccountId ?? defaultId) is not { } accountId)
        {
            yield break;
        }

        switch (row.Type)
        {
            case TransactionType.Income:
                yield return (accountId, row.Amount);
                break;
            case TransactionType.Expense:
                yield return (accountId, -row.Amount);
                break;
            case TransactionType.Transfer when row.TransferAccountId is { } toId:
                yield return (accountId, -row.Amount);
                yield return (toId, row.Amount);
                break;
        }
    }

    private sealed record Movement(int? AccountId, int? TransferAccountId, TransactionType Type, decimal Amount, DateTime OccurredAtUtc);
}
