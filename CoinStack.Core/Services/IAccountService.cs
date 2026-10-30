using CoinStack.Data.Entities;

namespace CoinStack.Services;

public sealed record AccountBalance(Account Account, decimal Balance);

public interface IAccountService
{
    /// <summary>Every account with its balance now, the main account first. Archived accounts only when asked for.</summary>
    Task<List<AccountBalance>> GetBalancesAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    /// <summary>The account money comes from and goes to when nothing else is chosen. Created on first use.</summary>
    Task<Account> GetDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Money available to spend: the balances of the cheque accounts. Savings, the emergency fund and
    /// cards are left out. Every page that shows money in the bank uses this, so they agree.
    /// </summary>
    Task<decimal> GetSpendableBalanceAsync(CancellationToken cancellationToken = default);

    /// <summary>How much the spendable balance moved on each day of the device's calendar in the range.</summary>
    Task<Dictionary<DateTime, decimal>> GetDailySpendableChangeAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);

    Task<Account> CreateAsync(Account account, CancellationToken cancellationToken = default);

    /// <summary>Saves the name, type and account number. The balance changes through <see cref="SetBalanceAsync"/>.</summary>
    Task UpdateAsync(Account account, CancellationToken cancellationToken = default);

    Task SetArchivedAsync(int accountId, bool archived, CancellationToken cancellationToken = default);

    Task SetDefaultAsync(int accountId, CancellationToken cancellationToken = default);

    /// <summary>"Update balance": the account held <paramref name="balance"/> at <paramref name="asOfUtc"/>.</summary>
    Task SetBalanceAsync(int accountId, decimal balance, DateTime asOfUtc, CancellationToken cancellationToken = default);

    Task<Transaction> TransferAsync(
        int fromAccountId,
        int toAccountId,
        decimal amount,
        DateTime occurredAtUtc,
        string? description = null,
        CancellationToken cancellationToken = default);
}
