using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class TransactionService : ITransactionService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly IGameLoopService _gameLoop;

    public TransactionService(
        IDbContextFactory<CoinStackDbContext> dbFactory,
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

    public async Task<List<Transaction>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return [];
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Transactions
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Transaction>> GetFromDateAsync(DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Transactions
            .AsNoTracking()
            .Where(x => x.OccurredAtUtc >= fromUtc)
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

    public async Task<(Transaction Transaction, GameTransactionResult Result)> CreateWithGameLoopAsync(
        Transaction transaction,
        int userTimezoneOffsetHours,
        CancellationToken cancellationToken = default)
    {
        var created = await CreateAsync(transaction, cancellationToken);
        var gameResult = await _gameLoop.ProcessTransactionAsync(created, userTimezoneOffsetHours, cancellationToken);
        return (created, gameResult);
    }

    public async Task UpdateAsync(Transaction transaction, int userTimezoneOffsetHours = 0, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Transactions.FirstOrDefaultAsync(x => x.Id == transaction.Id, cancellationToken);
        if (existing is null) return;

        // Check impact before committing to this change, if there is an impact, we need to revert it before applying the new one
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
        existing.ExpenseKind = transaction.ExpenseKind;

        if (oldDebtId.HasValue && oldDebtImpact > 0)
        {
            await AdjustDebtBalanceAsync(db, oldDebtId.Value, oldDebtImpact, cancellationToken);
        }

        if (newDebtId.HasValue && newDebtImpact > 0)
        {
            await AdjustDebtBalanceAsync(db, newDebtId.Value, -newDebtImpact, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        await _gameLoop.ProcessTransactionAsync(existing, userTimezoneOffsetHours, cancellationToken);
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

    public async Task<decimal> GetExpenseTotalForBudgetPeriodAsync(
        int monthStartDay,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var (startUtc, endUtc) = GetBudgetPeriodBoundsUtc(monthStartDay, utcNow);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var sum = await db.Transactions
            .AsNoTracking()
            .Where(t => t.Type == TransactionType.Expense
                        && t.OccurredAtUtc >= startUtc
                        && t.OccurredAtUtc < endUtc)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken);

        return sum ?? 0m;
    }

    public async Task<(decimal TotalIncome, decimal TotalExpense)> GetLifetimeTotalsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var income = await db.Transactions
            .AsNoTracking()
            .Where(t => t.Type == TransactionType.Income)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var expense = await db.Transactions
            .AsNoTracking()
            .Where(t => t.Type == TransactionType.Expense)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        return (income, expense);
    }

    public async Task<decimal> GetNetBalanceBeforeAsync(DateTime beforeUtc, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var income = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc < beforeUtc && t.Type == TransactionType.Income)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var expense = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc < beforeUtc && t.Type == TransactionType.Expense)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        return income - expense;
    }

    public async Task<(decimal Income, decimal Expense)> GetIncomeExpenseForPeriodAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var income = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc >= startUtc
                        && t.OccurredAtUtc < endUtc
                        && t.Type == TransactionType.Income)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var expense = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc >= startUtc
                        && t.OccurredAtUtc < endUtc
                        && t.Type == TransactionType.Expense)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        return (income, expense);
    }

    public async Task<List<BucketSpendSummary>> GetBucketSpendingForPeriodAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc >= startUtc
                        && t.OccurredAtUtc < endUtc
                        && t.Type == TransactionType.Expense
                        && t.BucketId.HasValue)
            .GroupBy(t => t.BucketId!.Value)
            .Select(g => new
            {
                BucketId = g.Key,
                Spent = g.Sum(t => t.Amount)
            })
            .OrderByDescending(x => x.Spent)
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new BucketSpendSummary(x.BucketId, x.Spent))
            .ToList();
    }

    public async Task<List<DailyNetSummary>> GetDailyNetForRangeAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var incomeRows = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc >= startUtc
                        && t.OccurredAtUtc < endUtc
                        && t.Type == TransactionType.Income)
            .GroupBy(t => new { t.OccurredAtUtc.Year, t.OccurredAtUtc.Month, t.OccurredAtUtc.Day })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                Total = g.Sum(t => t.Amount)
            })
            .ToListAsync(cancellationToken);

        var expenseRows = await db.Transactions
            .AsNoTracking()
            .Where(t => t.OccurredAtUtc >= startUtc
                        && t.OccurredAtUtc < endUtc
                        && t.Type == TransactionType.Expense)
            .GroupBy(t => new { t.OccurredAtUtc.Year, t.OccurredAtUtc.Month, t.OccurredAtUtc.Day })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                Total = g.Sum(t => t.Amount)
            })
            .ToListAsync(cancellationToken);

        var byDate = new Dictionary<DateTime, DailyNetSummary>();

        foreach (var row in incomeRows)
        {
            var date = new DateTime(row.Year, row.Month, row.Day, 0, 0, 0, DateTimeKind.Utc);
            byDate[date] = new DailyNetSummary(date, row.Total, 0m);
        }

        foreach (var row in expenseRows)
        {
            var date = new DateTime(row.Year, row.Month, row.Day, 0, 0, 0, DateTimeKind.Utc);
            if (byDate.TryGetValue(date, out var existing))
            {
                byDate[date] = existing with { Expense = row.Total };
            }
            else
            {
                byDate[date] = new DailyNetSummary(date, 0m, row.Total);
            }
        }

        return byDate.Values
            .OrderByDescending(x => x.Date)
            .ToList();
    }

    public async Task ApplyAutoDeductionsForBudgetPeriodAsync(
        int monthStartDay,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var (startUtc, endUtc) = GetBudgetPeriodBoundsUtc(monthStartDay, utcNow);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var templates = await db.Transactions
            .AsNoTracking()
            .Where(t => t.Type == TransactionType.Expense
                        && t.AutoDeduct
                        && t.AutoDeductTemplateId == null)
            .ToListAsync(cancellationToken);

        if (templates.Count == 0)
        {
            return;
        }

        foreach (var template in templates)
        {
            // If the template transaction itself falls within this period, treat it as already applied.
            if (template.OccurredAtUtc >= startUtc && template.OccurredAtUtc < endUtc)
            {
                continue;
            }

            var alreadyApplied = await db.Transactions
                .AsNoTracking()
                .AnyAsync(t => t.AutoDeductTemplateId == template.Id
                               && t.OccurredAtUtc >= startUtc
                               && t.OccurredAtUtc < endUtc, cancellationToken);

            if (alreadyApplied)
            {
                continue;
            }

            var autoTx = new Transaction
            {
                OccurredAtUtc = startUtc,
                Amount = template.Amount,
                Type = TransactionType.Expense,
                Description = template.Description,
                Notes = template.Notes,
                CategoryId = template.CategoryId,
                SubscriptionId = template.SubscriptionId,
                BucketId = template.BucketId,
                DebtAccountId = template.DebtAccountId,
                IsImpulse = false,
                ExpenseKind = template.ExpenseKind,
                AutoDeduct = false,
                AutoDeductTemplateId = template.Id,
            };

            db.Transactions.Add(autoTx);

            var createDebtImpact = GetDebtPaymentImpact(autoTx);
            if (autoTx.DebtAccountId.HasValue && createDebtImpact > 0)
            {
                await AdjustDebtBalanceAsync(db, autoTx.DebtAccountId.Value, -createDebtImpact, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static (DateTime StartUtc, DateTime EndUtc) GetBudgetPeriodBoundsUtc(int monthStartDay, DateTime utcNow)
    {
        if (monthStartDay is < 1 or > 28)
        {
            monthStartDay = 1;
        }

        var startThisMonth = new DateTime(utcNow.Year, utcNow.Month, monthStartDay, 0, 0, 0, DateTimeKind.Utc);
        var startUtc = utcNow.Day >= monthStartDay ? startThisMonth : startThisMonth.AddMonths(-1);
        var endUtc = startUtc.AddMonths(1);
        return (startUtc, endUtc);
    }

    private static decimal GetDebtPaymentImpact(Transaction transaction)
    {
        return transaction.Type == TransactionType.Expense && transaction.DebtAccountId.HasValue
            ? Math.Max(0m, transaction.Amount)
            : 0m;
    }

    private static async Task AdjustDebtBalanceAsync(
        CoinStackDbContext db,
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
