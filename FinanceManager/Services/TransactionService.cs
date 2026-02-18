using FinanceManager.Data;
using FinanceManager.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Services;

public sealed class TransactionService : ITransactionService
{
    private readonly IDbContextFactory<FinanceManagerDbContext> _dbFactory;
    private readonly IGameLoopService _gameLoop;

    public TransactionService(
        IDbContextFactory<FinanceManagerDbContext> dbFactory,
        IGameLoopService gameLoop)
    {
        _dbFactory = dbFactory;
        _gameLoop = gameLoop;
    }

    public async Task<List<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Transactions
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Transaction?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Transaction> CreateAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        var createDebtImpact = GetDebtPaymentImpact(transaction);
        if (transaction.DebtAccountId.HasValue && createDebtImpact > 0)
        {
            await AdjustDebtBalanceAsync(db, transaction.DebtAccountId.Value, -createDebtImpact, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return transaction;
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Transactions.FirstOrDefaultAsync(x => x.Id == transaction.Id, cancellationToken);
        if (existing is null) return;

        // Check impact before commiting to this change, if there is an impact, we need to revert it before applying the new one
        await _gameLoop.RevertTransactionImpactAsync(existing, cancellationToken);

        var oldDebtId = existing.DebtAccountId;
        var oldDebtImpact = GetDebtPaymentImpact(existing);
        var newDebtId = transaction.DebtAccountId;
        var newDebtImpact = GetDebtPaymentImpact(transaction);

        existing.OccurredAtUtc = transaction.OccurredAtUtc;
        existing.Amount = transaction.Amount;
        existing.Type = transaction.Type;
        existing.Description = transaction.Description;
        existing.Notes = transaction.Notes;
        existing.CategoryId = transaction.CategoryId;
        existing.SubscriptionId = transaction.SubscriptionId;
        existing.BucketId = transaction.BucketId;
        existing.DebtAccountId = transaction.DebtAccountId;
        existing.IsImpulse = transaction.IsImpulse;

        if (oldDebtId.HasValue && oldDebtImpact > 0)
        {
            await AdjustDebtBalanceAsync(db, oldDebtId.Value, oldDebtImpact, cancellationToken);
        }

        if (newDebtId.HasValue && newDebtImpact > 0)
        {
            await AdjustDebtBalanceAsync(db, newDebtId.Value, -newDebtImpact, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        int userOffset = 0;
        await _gameLoop.ProcessTransactionAsync(existing, userOffset, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Transactions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null) return;

        await _gameLoop.RevertTransactionImpactAsync(existing, cancellationToken);

        var debtImpact = GetDebtPaymentImpact(existing);
        if (existing.DebtAccountId.HasValue && debtImpact > 0)
        {
            await AdjustDebtBalanceAsync(db, existing.DebtAccountId.Value, debtImpact, cancellationToken);
        }

        db.Transactions.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static decimal GetDebtPaymentImpact(Transaction transaction)
    {
        return transaction.Type == TransactionType.Expense && transaction.DebtAccountId.HasValue
            ? Math.Max(0m, transaction.Amount)
            : 0m;
    }

    private static async Task AdjustDebtBalanceAsync(
        FinanceManagerDbContext db,
        int debtId,
        decimal balanceDelta,
        CancellationToken cancellationToken)
    {
        var debt = await db.DebtAccounts.FirstOrDefaultAsync(x => x.Id == debtId, cancellationToken);
        if (debt is null)
        {
            return;
        }

        var nextBalance = debt.CurrentBalance + balanceDelta;
        if (nextBalance < 0m)
        {
            nextBalance = 0m;
        }

        if (nextBalance > debt.TotalAmount)
        {
            nextBalance = debt.TotalAmount;
        }

        debt.CurrentBalance = nextBalance;
    }
}
