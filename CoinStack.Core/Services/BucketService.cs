using CoinStack.Data;
using CoinStack.Data.Entities;
using CoinStack.Services.Import;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services;

public sealed class BucketService : IBucketService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly TimeProvider _clock;

    public BucketService(IDbContextFactory<CoinStackDbContext> dbFactory, TimeProvider? clock = null)
    {
        _dbFactory = dbFactory;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<List<Bucket>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Buckets
            .AsNoTracking()
            .Include(x => x.Category)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Bucket?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Buckets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Bucket> CreateAsync(Bucket bucket, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Buckets.Add(bucket);
        await db.SaveChangesAsync(cancellationToken);
        return bucket;
    }

    public async Task UpdateAsync(Bucket bucket, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.Buckets.FirstOrDefaultAsync(x => x.Id == bucket.Id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        existing.Name = bucket.Name;
        existing.AllocatedAmount = bucket.AllocatedAmount;
        existing.CategoryId = bucket.CategoryId;
        existing.Icon = bucket.Icon;
        existing.SortOrder = bucket.SortOrder;
        existing.IsSavings = bucket.IsSavings;
        existing.GoalId = bucket.IsSavings ? bucket.GoalId : null;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Buckets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        // Only the bucket's own name: a bucket like "Groceries" holds many merchants that must stay scannable.
        await RecurringDismissals.RecordAsync(db, existing.Name, [], subscription: false, bucket: true, cancellationToken);

        db.Buckets.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<int, decimal>> GetSpentAmountsAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var startUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = startUtc.AddMonths(1);

        return await db.Transactions
            .AsNoTracking()
            .Where(t => t.BucketId != null
                        && t.Type == TransactionType.Expense
                        && t.OccurredAtUtc >= startUtc
                        && t.OccurredAtUtc < endUtc)
            .GroupBy(t => t.BucketId!.Value)
            .Select(g => new { BucketId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.BucketId, x => x.Total, cancellationToken);
    }

    public async Task<Dictionary<int, decimal>> GetSpentAmountsForPeriodAsync(int monthStartDay, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var (_, startUtc, endUtc) = BudgetPeriod.Containing(monthStartDay, utcNow, _clock.LocalTimeZone);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Transactions
            .AsNoTracking()
            .Where(t => t.BucketId != null
                        && t.Type == TransactionType.Expense
                        && t.OccurredAtUtc >= startUtc
                        && t.OccurredAtUtc < endUtc)
            .GroupBy(t => t.BucketId!.Value)
            .Select(g => new { BucketId = g.Key, Total = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.BucketId, x => x.Total, cancellationToken);
    }
}
