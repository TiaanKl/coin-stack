using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services.Import;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

/// <summary>After a bank transaction moved category: the merchant, where it now goes, and how many of its other transactions don't.</summary>
public sealed record MerchantFollowUp(string MerchantName, string CategoryName, int OtherTransactions);

/// <summary>
/// The rest of "learn from my category changes": once a merchant's category was changed on one transaction
/// (which <see cref="ITransactionService.UpdateAsync"/> remembers for future imports), offer to move the
/// merchant's earlier transactions too.
/// </summary>
public interface IMerchantRuleService
{
    /// <summary>Null when the transaction didn't come from a bank statement or has no category.</summary>
    Task<MerchantFollowUp?> GetFollowUpAsync(int transactionId, CancellationToken cancellationToken = default);

    /// <summary>Moves the merchant's other bank transactions to this transaction's category. Returns how many moved.</summary>
    Task<int> ApplyToMerchantAsync(int transactionId, CancellationToken cancellationToken = default);
}

public sealed class MerchantRuleService : IMerchantRuleService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public MerchantRuleService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<MerchantFollowUp?> GetFollowUpAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var (transaction, others) = await LoadMerchantAsync(db, transactionId, cancellationToken);
        if (transaction?.Category is null)
        {
            return null;
        }

        return new MerchantFollowUp(transaction.Description, transaction.Category.Name, others.Count);
    }

    public async Task<int> ApplyToMerchantAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var (transaction, others) = await LoadMerchantAsync(db, transactionId, cancellationToken);
        if (transaction?.CategoryId is not { } categoryId)
        {
            return 0;
        }

        foreach (var other in others)
        {
            other.CategoryId = categoryId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return others.Count;
    }

    /// <summary>The bank transaction, plus the same merchant's other bank transactions in a different category.</summary>
    private static async Task<(Transaction? Transaction, List<Transaction> Others)> LoadMerchantAsync(
        CoinStackDbContext db,
        int transactionId,
        CancellationToken cancellationToken)
    {
        var transaction = await db.Transactions
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == transactionId, cancellationToken);
        if (transaction is null || transaction.Source != TransactionConventions.BankSource || transaction.CategoryId is null)
        {
            return (null, []);
        }

        // Keys come from the bank's wording, which SQL can't normalise, so the candidates are matched in memory.
        var key = MerchantRuleBook.KeyOf(transaction);
        var others = (await db.Transactions
                .Where(t => t.Id != transactionId
                            && t.Source == TransactionConventions.BankSource
                            && t.Type == transaction.Type
                            && t.CategoryId != transaction.CategoryId)
                .ToListAsync(cancellationToken))
            .Where(t => MerchantRuleBook.KeyOf(t) == key)
            .ToList();

        return (transaction, others);
    }
}
