using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Mobile.Services;

public sealed class MobileFinanceService : IMobileFinanceService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;

    public MobileFinanceService(IDbContextFactory<CoinStackDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<MobileDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var transactions = await db.Transactions
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        var totalIncome = await db.Transactions
            .Where(x => x.Type == TransactionType.Income)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var totalExpense = await db.Transactions
            .Where(x => x.Type == TransactionType.Expense)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var buckets = await db.Buckets
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return new MobileDashboardSnapshot(
            totalIncome,
            totalExpense,
            totalIncome - totalExpense,
            transactions,
            buckets);
    }

    public async Task<IReadOnlyList<Transaction>> GetTransactionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Transactions
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddTransactionAsync(decimal amount, TransactionType type, string description, int? bucketId = null, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new InvalidOperationException("Amount must be greater than 0.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        db.Transactions.Add(new Transaction
        {
            Amount = amount,
            Type = type,
            Description = description?.Trim() ?? string.Empty,
            BucketId = bucketId,
            OccurredAtUtc = DateTime.UtcNow,
            ExpenseKind = type == TransactionType.Expense ? ExpenseKind.Discretionary : ExpenseKind.Mandatory
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Bucket>> GetBucketsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Buckets
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddBucketAsync(string name, decimal allocatedAmount, bool isSavings, CancellationToken cancellationToken = default)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new InvalidOperationException("Bucket name is required.");
        }

        if (allocatedAmount < 0)
        {
            throw new InvalidOperationException("Allocated amount cannot be negative.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var nextOrder = await db.Buckets.AnyAsync(cancellationToken)
            ? await db.Buckets.MaxAsync(x => x.SortOrder, cancellationToken) + 1
            : 1;

        db.Buckets.Add(new Bucket
        {
            Name = trimmedName,
            AllocatedAmount = allocatedAmount,
            IsSavings = isSavings,
            SortOrder = nextOrder
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var settings = await db.AppSettings
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return settings ?? new AppSettings();
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.Currency))
        {
            throw new InvalidOperationException("Currency is required.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.AppSettings.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            db.AppSettings.Add(new AppSettings
            {
                Currency = settings.Currency.Trim().ToUpperInvariant(),
                MonthStartDay = settings.MonthStartDay,
                MonthlyIncome = settings.MonthlyIncome,
                EnableReflections = settings.EnableReflections,
                EnableScoring = settings.EnableScoring,
                EnableStreaks = settings.EnableStreaks,
                EnableToast = settings.EnableToast,
                LargeExpenseThreshold = settings.LargeExpenseThreshold,
                SavingsIsPercent = settings.SavingsIsPercent,
                MonthlySavingsAmount = settings.MonthlySavingsAmount,
                MonthlySavingsPercent = settings.MonthlySavingsPercent,
                SavingsInterestRate = settings.SavingsInterestRate,
                SavingsInterestIsYearly = settings.SavingsInterestIsYearly
            });
        }
        else
        {
            existing.Currency = settings.Currency.Trim().ToUpperInvariant();
            existing.MonthStartDay = settings.MonthStartDay;
            existing.MonthlyIncome = settings.MonthlyIncome;
            existing.EnableReflections = settings.EnableReflections;
            existing.EnableScoring = settings.EnableScoring;
            existing.EnableStreaks = settings.EnableStreaks;
            existing.EnableToast = settings.EnableToast;
            existing.LargeExpenseThreshold = settings.LargeExpenseThreshold;
            existing.SavingsIsPercent = settings.SavingsIsPercent;
            existing.MonthlySavingsAmount = settings.MonthlySavingsAmount;
            existing.MonthlySavingsPercent = settings.MonthlySavingsPercent;
            existing.SavingsInterestRate = settings.SavingsInterestRate;
            existing.SavingsInterestIsYearly = settings.SavingsInterestIsYearly;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
